// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogAssemblyCatalogDefinitionProcessingService
{
    private void ValidateCatalogDefinition(CatalogDefinition catalogDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogDefinition);
        var tables = catalogDefinition.TablesSource;
        var indexes = catalogDefinition.IndexesSource;
        var scalarTypes = catalogDefinition.ScalarTypesSource;
        var tableTypes = catalogDefinition.TableTypesSource;
        var xmlSchemaCollections = catalogDefinition.XmlSchemaCollectionsSource;
        var assemblies = catalogDefinition.AssembliesSource;
        CatalogTable[] _tables;
        CatalogIndex[] _indexes;
        CatalogScalarType[] _scalarTypes;
        CatalogTableType[] _tableTypes;
        SqlXmlSchemaCollection[] _xmlSchemaCollections;
        CatalogAssembly[] _assemblies;
        ArgumentNullException.ThrowIfNull(argument: tables);
        ArgumentNullException.ThrowIfNull(argument: indexes);
        _tables = tables.ToArray();
        _indexes = indexes.ToArray();
        _scalarTypes = scalarTypes?.ToArray() ?? [];
        _tableTypes = tableTypes?.ToArray() ?? [];

        var referencedXml = _tables.SelectMany(selector: table => table.Columns)
            .Concat(second: _tableTypes.SelectMany(selector: type => type.Columns))
            .Select(selector: column => column.Type.XmlSchemaCollection)
            .Where(predicate: collection => collection is not null)
            .Cast<SqlXmlSchemaCollection>()
            .Concat(second: _scalarTypes.Select(selector: type => type.Definition.XmlSchemaCollection)
            .Where(predicate: collection => collection is not null)
            .Cast<SqlXmlSchemaCollection>())
            .ToArray();

        _xmlSchemaCollections = xmlSchemaCollections?.ToArray() ?? referencedXml.GroupBy(keySelector: collection => (collection.SchemaName, collection.Name))
            .Select(selector: group => group.First())
            .ToArray();

        var referencedAssemblies = _scalarTypes.Where(predicate: type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(selector: type => type.Definition.ClrAssemblyName!)
            .Distinct(comparer: StringComparer.Ordinal);

        _assemblies = assemblies?.Select(selector: assembliesMetadata.PrepareCatalogAssembly)
            .ToArray() ?? referencedAssemblies.Select(selector: name => assembliesMetadata.PrepareCatalogAssembly(catalogAssembly: new CatalogAssembly { Name = name, ImageContent = "", PermissionSet = CatalogAssemblyPermissionSet.Safe }))
            .ToArray();

        if (_tables.Any(predicate: table => table is null))
        {
            throw new ArgumentException(message: "Tables cannot contain null records.", paramName: nameof(tables));
        }

        if (_indexes.Any(predicate: index => index is null))
        {
            throw new ArgumentException(message: "Indexes cannot contain null records.", paramName: nameof(indexes));
        }

        if (_scalarTypes.Any(predicate: type => type is null))
        {
            throw new ArgumentException(message: "Scalar types cannot contain null records.", paramName: nameof(scalarTypes));
        }

        if (_tableTypes.Any(predicate: type => type is null))
        {
            throw new ArgumentException(message: "Table types cannot contain null records.", paramName: nameof(tableTypes));
        }

        ValidateUnique(values: _xmlSchemaCollections.Select(selector: collection => (collection.SchemaName, collection.Name)), description: "XML schema collection names", parameterName: nameof(xmlSchemaCollections));
        ValidateUnique(values: _assemblies.Select(selector: assembly => assembly.Name), description: "Assembly names", parameterName: nameof(assemblies), comparer: StringComparer.Ordinal);

        if (_tables.Any(predicate: table => table.ObjectId == Guid.Empty))
        {
            throw new ArgumentException(message: "Object identities must be nonempty.", paramName: nameof(tables));
        }

        ValidateUnique(values: _tables.Select(selector: table => table.ObjectId), description: "Security object identities", parameterName: nameof(tables));
        ValidateUnique(values: _tables.Select(selector: table => table.Id), description: "Table IDs", parameterName: nameof(tables));
        ValidateUnique(values: _tables.Select(selector: table => (table.DatabaseName, table.SchemaName, table.Name)), description: "Qualified table names", parameterName: nameof(tables));
        ValidateUnique(values: _indexes.Select(selector: index => index.Id), description: "Index IDs", parameterName: nameof(indexes));

        var typeNames = _scalarTypes.Select(selector: type => (type.Definition.UserTypeSchema!, type.Definition.UserTypeName!))
            .Concat(second: _tableTypes.Select(selector: type => (type.SchemaName, type.Name)))
            .ToArray();

        ValidateUnique(values: typeNames, description: "User-defined type names", parameterName: nameof(scalarTypes));

        ValidateUnique(values: _scalarTypes.Where(predicate: type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(selector: type => (type.Definition.ClrAssemblyName!, type.Definition.ClrClassName!)), description: "CLR assembly/class bindings", parameterName: nameof(scalarTypes));

        var xmlByName = _xmlSchemaCollections.ToDictionary(keySelector: collection => (collection.SchemaName, collection.Name));

        foreach (var collection in referencedXml)
        {
            if (!xmlByName.TryGetValue(key: (collection.SchemaName, collection.Name), value: out var declared) || !declared.Equals(other: collection))
            {
                throw new ArgumentException(message: $"Typed XML references undeclared or mismatched collection {$"[{collection.SchemaName}].[{collection.Name}]"}.", paramName: nameof(xmlSchemaCollections));
            }
        }

        var assemblyNames = _assemblies.Select(selector: assembly => assembly.Name)
            .ToHashSet(comparer: StringComparer.Ordinal);

        foreach (var scalar in _scalarTypes.Where(predicate: type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr))
        {
            if (!assemblyNames.Contains(item: scalar.Definition.ClrAssemblyName!))
            {
                throw new ArgumentException(message: $"CLR type {$"[{scalar.Definition.UserTypeSchema}].[{scalar.Definition.UserTypeName}]"} references an undeclared assembly.", paramName: nameof(assemblies));
            }
        }

        var scalarTypesByName = _scalarTypes.ToDictionary(keySelector: type => (type.Definition.UserTypeSchema!, type.Definition.UserTypeName!));

        foreach (var column in _tables.SelectMany(selector: table => table.Columns)
            .Concat(second: _tableTypes.SelectMany(selector: type => type.Columns)))
        {
            if (!IsUserDefined(type: column.Type))
            {
                continue;
            }

            var name = (column.Type.UserTypeSchema!, column.Type.UserTypeName!);

            if (!scalarTypesByName.TryGetValue(key: name, value: out var declared) || declared.Definition != column.Type)
            {
                throw new ArgumentException(message: $"Column '{column.Name}' references an undeclared or mismatched user-defined type.", paramName: nameof(scalarTypes));
            }
        }

        var tablesById = _tables.ToDictionary(keySelector: table => table.Id);
        var indexesById = _indexes.ToDictionary(keySelector: index => index.Id);

        foreach (var table in _tables.Where(predicate: table => table.Graph is not null))
        {
            var graph = table.Graph!;

            if (table.SystemVersioning is not null)
            {
                throw new ArgumentException(message: "Graph tables cannot also be system-versioned.", paramName: nameof(tables));
            }

            var identity = RequireGraphColumn(table: table, columnId: graph.IdentityColumnId, generatedAlways: CatalogGeneratedAlwaysKind.GraphIdentity, parameterName: nameof(tables));

            if ((graph.Kind == GraphTableKind.Node && table.Columns[^1].Id != identity.Id) || (graph.Kind == GraphTableKind.Edge && table.Columns[^3].Id != identity.Id))
            {
                throw new ArgumentException(message: "Graph identity columns must occupy the documented trailing position.", paramName: nameof(tables));
            }

            RequireGraphIndex(table: table, indexId: graph.IdentityIndexId, columnId: identity.Id, unique: true, indexes: indexesById, parameterName: nameof(indexes));

            if (graph.Kind == GraphTableKind.Node)
            {
                continue;
            }

            if (!tablesById.TryGetValue(key: graph.FromNodeTableId!.Value, value: out var fromTable) || fromTable.Graph?.Kind != GraphTableKind.Node || !tablesById.TryGetValue(key: graph.ToNodeTableId!.Value, value: out var toTable) || toTable.Graph?.Kind != GraphTableKind.Node)
            {
                throw new ArgumentException(message: "Graph edge endpoints must reference registered node tables.", paramName: nameof(tables));
            }

            var from = RequireGraphColumn(table: table, columnId: graph.FromNodeColumnId!.Value, generatedAlways: CatalogGeneratedAlwaysKind.GraphFromNode, parameterName: nameof(tables));
            var to = RequireGraphColumn(table: table, columnId: graph.ToNodeColumnId!.Value, generatedAlways: CatalogGeneratedAlwaysKind.GraphToNode, parameterName: nameof(tables));

            if (table.Columns[^2].Id != from.Id || table.Columns[^1].Id != to.Id)
            {
                throw new ArgumentException(message: "Graph edge endpoint columns must be the final two columns.", paramName: nameof(tables));
            }

            RequireGraphIndex(table: table, indexId: graph.OutgoingIndexId!.Value, columnId: from.Id, unique: false, indexes: indexesById, parameterName: nameof(indexes));
            RequireGraphIndex(table: table, indexId: graph.IncomingIndexId!.Value, columnId: to.Id, unique: false, indexes: indexesById, parameterName: nameof(indexes));
        }

        foreach (var current in _tables.Where(predicate: table => table.SystemVersioning is not null))
        {
            var temporal = current.SystemVersioning!;

            if (!tablesById.TryGetValue(key: temporal.HistoryTableId, value: out var history) || history.Id == current.Id)
            {
                throw new ArgumentException(message: $"System-versioned table '{current.Name}' references an invalid history table.", paramName: nameof(tables));
            }

            if (history.SystemVersioning is not null)
            {
                throw new ArgumentException(message: "A temporal history table cannot itself be system-versioned.", paramName: nameof(tables));
            }

            if (_tables.Count(predicate: table => table.SystemVersioning?.HistoryTableId == history.Id) != 1)
            {
                throw new ArgumentException(message: "A temporal history table must belong to exactly one current table.", paramName: nameof(tables));
            }

            if (current.Columns.Count != history.Columns.Count || current.Columns.Zip(second: history.Columns)
            .Any(predicate: pair => pair.First.Id != pair.Second.Id || pair.First.Name != pair.Second.Name || pair.First.Type != pair.Second.Type || pair.First.IsNullable != pair.Second.IsNullable))
            {
                throw new ArgumentException(message: "A temporal history table must have the current table's ordered column schema.", paramName: nameof(tables));
            }
        }

        foreach (var group in _indexes.GroupBy(keySelector: index => index.TableId))
        {
            ValidateUnique(values: group.Select(selector: index => index.Name), description: "Index names", parameterName: nameof(indexes), comparer: StringComparer.Ordinal);

            if (group.Count(predicate: index => index.StorageKind == CatalogIndexStorageKind.Clustered) > 1)
            {
                throw new ArgumentException(message: "A disk table can have only one clustered index.", paramName: nameof(indexes));
            }

            if (group.Count(predicate: index => index.StorageKind == CatalogIndexStorageKind.NonClustered) > 999)
            {
                throw new ArgumentException(message: "A disk table can have at most 999 nonclustered indexes.", paramName: nameof(indexes));
            }

            if (group.Count(predicate: index => index.IsPrimaryKey) > 1)
            {
                throw new ArgumentException(message: "A table can have only one primary key.", paramName: nameof(indexes));
            }
        }

        foreach (var table in _tables.Where(predicate: table => table.Columns.Any(predicate: column => column.IsFileStream)))
        {
            var rowGuid = table.Columns.Single(predicate: column => column.IsRowGuidCol);

            if (!_indexes.Any(predicate: index => index.TableId == table.Id && index.Method == CatalogIndexMethod.BTree && index.IsUnique && index.Columns.Count == 1 && index.Columns[0].ColumnId == rowGuid.Id))
            {
                throw new ArgumentException(message: "FILESTREAM requires a single-column UNIQUE or PRIMARY KEY index on the ROWGUIDCOL column.", paramName: nameof(indexes));
            }
        }

        foreach (var index in _indexes)
        {
            if (!tablesById.TryGetValue(key: index.TableId, value: out var table))
            {
                throw new ArgumentException(message: $"Index '{index.Name}' references an unknown table.", paramName: nameof(indexes));
            }

            var columnIds = table.Columns.Select(selector: column => column.Id)
            .ToHashSet();

            if (index.Columns.Any(predicate: column => !columnIds.Contains(item: column.ColumnId)))
            {
                throw new ArgumentException(message: $"Index '{index.Name}' references an unknown column.", paramName: nameof(indexes));
            }

            if (index.IncludedColumns.Any(predicate: column => !columnIds.Contains(item: column)))
            {
                throw new ArgumentException(message: $"Index '{index.Name}' includes an unknown column.", paramName: nameof(indexes));
            }

            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Count > 32)
            {
                throw new ArgumentException(message: "A rowstore index key can contain at most 32 columns.", paramName: nameof(indexes));
            }

            if (index.IncludedColumns.Count > 1_023)
            {
                throw new ArgumentException(message: "A nonclustered index can contain at most 1,023 included columns.", paramName: nameof(indexes));
            }

            foreach (var includedId in index.IncludedColumns)
            {
                var included = table.Columns.Single(predicate: column => column.Id == includedId);

                if (included.Type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image || included.IsColumnSet)
                {
                    throw new ArgumentException(message: $"Column '{included.Name}' cannot be included in an index.", paramName: nameof(indexes));
                }
            }

            foreach (var indexedColumn in index.Columns)
            {
                var column = table.Columns.Single(predicate: candidate => candidate.Id == indexedColumn.ColumnId);

                if (column.IsColumnSet)
                {
                    throw new ArgumentException(message: "An XML column set cannot be indexed.", paramName: nameof(indexes));
                }

                if (column.IsComputed && index.Method == CatalogIndexMethod.Json)
                {
                    throw new ArgumentException(message: "A JSON index cannot be created on a computed JSON column.", paramName: nameof(indexes));
                }

                if (column.IsComputed && index.Method == CatalogIndexMethod.Xml && index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Primary or CatalogXmlIndexKind.Selective)
                {
                    throw new ArgumentException(message: "A primary or selective XML index cannot be created on a computed XML column.", paramName: nameof(indexes));
                }

                if (column.IsComputed && index.Method == CatalogIndexMethod.FullText)
                {
                    throw new ArgumentException(message: "A full-text index cannot be created on a computed column.", paramName: nameof(indexes));
                }

                var compatible = index.Method switch
                {
                    CatalogIndexMethod.BTree => CanBeBTreeKey(type: column.Type),
                    CatalogIndexMethod.Json => column.Type.Name == SqlTypeName.Json,
                    CatalogIndexMethod.Spatial => column.Type.Name is SqlTypeName.Geometry or SqlTypeName.Geography,
                    CatalogIndexMethod.Vector => column.Type.Name == SqlTypeName.Vector,
                    CatalogIndexMethod.Xml => column.Type.Name == SqlTypeName.Xml,
                    CatalogIndexMethod.FullText => column.Type.Name is SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText,
                    _ => false
                };

                if (!compatible)
                {
                    throw new ArgumentException(message: $"SQL Server type {FormatType(type: column.Type)} cannot be a B-tree key.", paramName: nameof(indexes));
                }

                if (column.Encryption?.EncryptionType == CatalogEncryptionType.Randomized)
                {
                    throw new ArgumentException(message: "Randomized encrypted columns cannot be index keys.", paramName: nameof(indexes));
                }

                if (column.IsSparse && (index.StorageKind == CatalogIndexStorageKind.Clustered || index.IsPrimaryKey))
                {
                    throw new ArgumentException(message: "A SPARSE column cannot be part of a clustered index or primary key.", paramName: nameof(indexes));
                }

                if (index.Method == CatalogIndexMethod.FullText && !StringComparer.OrdinalIgnoreCase.Equals(x: column.Type.Collation, y: index.SpecializedOptions!.FullTextOptions!.Collation))
                {
                    throw new ArgumentException(message: $"Full-text index '{index.Name}' collation must exactly match its source column collation.", paramName: nameof(indexes));
                }
            }

            var maximumKeyBytes = index.StorageKind == CatalogIndexStorageKind.Clustered ? sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumClusteredKeyBytes : sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes;

            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Sum(selector: indexedColumn => MinimumIndexKeyBytes(type: table.Columns.Single(predicate: column => column.Id == indexedColumn.ColumnId)
            .Type)) > maximumKeyBytes)
            {
                throw new ArgumentException(message: "The fixed-width portion of the composite index key exceeds the SQL Server index-kind limit.", paramName: nameof(indexes));
            }

            if (index.IsPrimaryKey && index.Columns.Any(predicate: indexed => table.Columns.Single(predicate: column => column.Id == indexed.ColumnId)
            .IsNullable))
            {
                throw new ArgumentException(message: "Primary-key columns cannot be nullable.", paramName: nameof(indexes));
            }

            if (index.Method is CatalogIndexMethod.Json or CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector or CatalogIndexMethod.Xml or CatalogIndexMethod.FullText)
            {
                var clusteringKey = _indexes.SingleOrDefault(predicate: candidate => candidate.TableId == table.Id && candidate.IsPrimaryKey && candidate.StorageKind == CatalogIndexStorageKind.Clustered);

                if (clusteringKey is null)
                {
                    throw new ArgumentException(message: "Specialized indexes require a clustered primary key.", paramName: nameof(indexes));
                }

                var clusteringKeyBytes = clusteringKey.Columns.Sum(selector: key => MaximumIndexKeyBytes(type: table.Columns.Single(predicate: column => column.Id == key.ColumnId)
            .Type));

                if (index.Method == CatalogIndexMethod.Json && (clusteringKey.Columns.Count > 31 || clusteringKeyBytes >= 128))
                {
                    throw new ArgumentException(message: "A JSON index requires at most 31 clustering-key columns totaling less than 128 bytes.", paramName: nameof(indexes));
                }

                if (index.Method == CatalogIndexMethod.Spatial && (clusteringKey.Columns.Count > 15 || clusteringKeyBytes > 895))
                {
                    throw new ArgumentException(message: "A spatial index requires at most 15 primary-key columns totaling at most 895 bytes.", paramName: nameof(indexes));
                }

                if (index.Method == CatalogIndexMethod.Xml && clusteringKey.Columns.Count > 15)
                {
                    throw new ArgumentException(message: "An XML index requires at most 15 clustering-key columns.", paramName: nameof(indexes));
                }
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.Json)
            .GroupBy(keySelector: index => index.TableId))
        {
            if (group.Count() > 249)
            {
                throw new ArgumentException(message: "A table can have at most 249 JSON indexes.", paramName: nameof(indexes));
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.Xml)
            .GroupBy(keySelector: index => index.TableId))
        {
            if (group.Count() > 249)
            {
                throw new ArgumentException(message: "A table can have at most 249 XML indexes.", paramName: nameof(indexes));
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.FullText)
            .GroupBy(keySelector: index => (index.TableId, index.Columns.Single()
            .ColumnId)))
        {
            if (group.Count() > 1)
            {
                throw new ArgumentException(message: "A textual column can have only one full-text index.", paramName: nameof(indexes));
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.Spatial)
            .GroupBy(keySelector: index => (index.TableId, index.Columns.Single()
            .ColumnId)))
        {
            if (group.Count() > 249)
            {
                throw new ArgumentException(message: "A spatial column can have at most 249 spatial indexes.", paramName: nameof(indexes));
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.Json)
            .GroupBy(keySelector: index => (index.TableId, index.Columns.Single()
            .ColumnId)))
        {
            if (group.Count() > 1)
            {
                throw new ArgumentException(message: "A JSON column can have only one JSON index.", paramName: nameof(indexes));
            }
        }

        foreach (var group in _indexes.Where(predicate: index => index.Method == CatalogIndexMethod.Xml)
            .GroupBy(keySelector: index => (index.TableId, index.Columns.Single()
            .ColumnId)))
        {
            if (group.Count(predicate: index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary) > 1)
            {
                throw new ArgumentException(message: "An XML column can have only one primary XML index.", paramName: nameof(indexes));
            }

            if (group.Count(predicate: index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective) > 1)
            {
                throw new ArgumentException(message: "An XML column can have only one selective XML index.", paramName: nameof(indexes));
            }

            if (group.Any(predicate: index => index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Path or CatalogXmlIndexKind.Value or CatalogXmlIndexKind.Property) && !group.Any(predicate: index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary))
            {
                throw new ArgumentException(message: "Secondary PATH, VALUE, and PROPERTY XML indexes require a primary XML index.", paramName: nameof(indexes));
            }
        }

        catalogDefinition.Tables = Array.AsReadOnly(array: _tables);
        catalogDefinition.Indexes = Array.AsReadOnly(array: _indexes);
        catalogDefinition.ScalarTypes = Array.AsReadOnly(array: _scalarTypes);
        catalogDefinition.TableTypes = Array.AsReadOnly(array: _tableTypes);
        catalogDefinition.XmlSchemaCollections = Array.AsReadOnly(array: _xmlSchemaCollections);
        catalogDefinition.Assemblies = Array.AsReadOnly(array: _assemblies);

        catalogDefinition.Security = new sql_storage_engine.Models.Security.StorageSecurityState
        {
            Revision = 1,
            Principals = [],
            Permissions = [],
            Audit = [],
            RetiredPrincipals = [],
            DatabasePermissions = [],
            PrincipalDependencies = []
        };
    }

    private static CatalogColumn RequireGraphColumn(CatalogTable table, ColumnId columnId, CatalogGeneratedAlwaysKind generatedAlways, string parameterName)
    {
        var column = table.Columns.SingleOrDefault(predicate: candidate => candidate.Id == columnId) ?? throw new ArgumentException(message: "Graph metadata references an unknown column.", paramName: parameterName);

        if (column.Type != new SqlType
        {
            Name = SqlTypeName.Binary,
            Length = 50
        }

        || column.IsNullable || !column.IsHidden || column.GeneratedAlways != generatedAlways || column.DefaultExpression is not null || column.Identity is not null || column.IsComputed || column.Encryption is not null)
        {
            throw new ArgumentException(message: $"Graph column '{column.Name}' must be hidden, non-null binary({50}) with its exact generated-always kind.", paramName: parameterName);
        }

        return column;
    }

    private static void RequireGraphIndex(CatalogTable table, IndexId indexId, ColumnId columnId, bool unique, IReadOnlyDictionary<IndexId, CatalogIndex> indexes, string parameterName)
    {
        if (!indexes.TryGetValue(key: indexId, value: out var index) || index.TableId != table.Id || index.Method != CatalogIndexMethod.BTree || index.IsUnique != unique || index.Columns.Count != 1 || index.Columns[0].ColumnId != columnId)
        {
            throw new ArgumentException(message: "Graph metadata references an incompatible identity or adjacency index.", paramName: parameterName);
        }
    }

    private static int MaximumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MaximumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr when type.ClrMaxByteSize is null => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes,
        SqlUserTypeKind.Clr => type.ClrMaxByteSize is> 0 and <= sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes ? type.ClrMaxByteSize.Value : sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1,
        _ => type.Name switch
        {
            SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
            SqlTypeName.SmallInt => 2,
            SqlTypeName.Int or SqlTypeName.Real or SqlTypeName.SmallMoney or SqlTypeName.SmallDateTime => 4,
            SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.DateTime => 8,
            SqlTypeName.Float => type.Precision <= 24 ? 4 : 8,
            SqlTypeName.Date => 3,
            SqlTypeName.Time => type.Scale <= 2 ? 3 : type.Scale <= 4 ? 4 : 5,
            SqlTypeName.DateTime2 => type.Scale <= 2 ? 6 : type.Scale <= 4 ? 7 : 8,
            SqlTypeName.DateTimeOffset => type.Scale <= 2 ? 8 : type.Scale <= 4 ? 9 : 10,
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 : type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => type.Length!.Value,
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(type.Length!.Value * 2),
            SqlTypeName.HierarchyId => 892,
            SqlTypeName.SqlVariant => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumSqlVariantKeyBytes,
            _ => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1
        }};

    private static int MinimumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MinimumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr => type.ClrIsFixedLength && type.ClrMaxByteSize is> 0 ? type.ClrMaxByteSize.Value : 0,
        _ => type.Name switch
        {
            SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary or SqlTypeName.HierarchyId or SqlTypeName.SqlVariant => 0,
            _ => MaximumIndexKeyBytes(type: type)}};

    private static void ValidateUnique<T>(IEnumerable<T> values, string description, string parameterName, IEqualityComparer<T>? comparer = null)
    {
        var materialized = values.ToArray();

        if (materialized.Distinct(comparer: comparer)
            .Count() != materialized.Length)
        {
            throw new ArgumentException(message: $"{description} must be unique within their scope.", paramName: parameterName);
        }
    }

    private static bool IsUserDefined(SqlType type) =>
        type.UserTypeKind != SqlUserTypeKind.None;

    private static bool CanBeBTreeKey(SqlType type) =>
        type.UserTypeKind == SqlUserTypeKind.Clr ? type.ClrIsByteOrdered : type.Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when type.IsMax => false,
        SqlTypeName.Cursor or SqlTypeName.Table => false,
        _ => true
    };

    private static string FormatType(SqlType type) =>
        type.UserTypeKind != SqlUserTypeKind.None ? $"[{type.UserTypeSchema}].[{type.UserTypeName}]" : type.Name.ToString();
}
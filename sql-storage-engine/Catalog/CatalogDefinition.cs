using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using System.Numerics;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace sql_storage_engine.Catalog;

public sealed class CatalogDefinition
{
    internal Security.StorageSecurityState Security { get; set; } = new();
    private readonly CatalogTable[] _tables;
    private readonly CatalogIndex[] _indexes;
    private readonly CatalogScalarType[] _scalarTypes;
    private readonly CatalogTableType[] _tableTypes;
    private readonly SqlXmlSchemaCollection[] _xmlSchemaCollections;
    private readonly CatalogAssembly[] _assemblies;

    public CatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes,
        IEnumerable<CatalogScalarType>? scalarTypes = null, IEnumerable<CatalogTableType>? tableTypes = null,
        IEnumerable<SqlXmlSchemaCollection>? xmlSchemaCollections = null,
        IEnumerable<CatalogAssembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(indexes);
        _tables = tables.ToArray();
        _indexes = indexes.ToArray();
        _scalarTypes = scalarTypes?.ToArray() ?? [];
        _tableTypes = tableTypes?.ToArray() ?? [];
        var referencedXml = _tables.SelectMany(table => table.Columns).Concat(_tableTypes.SelectMany(type => type.Columns))
            .Select(column => column.Type.XmlSchemaCollection).Where(collection => collection is not null).Cast<SqlXmlSchemaCollection>()
            .Concat(_scalarTypes.Select(type => type.Definition.XmlSchemaCollection).Where(collection => collection is not null)
                .Cast<SqlXmlSchemaCollection>()).ToArray();
        _xmlSchemaCollections = xmlSchemaCollections?.ToArray() ?? referencedXml
            .GroupBy(collection => (collection.SchemaName, collection.Name)).Select(group => group.First()).ToArray();
        var referencedAssemblies = _scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(type => type.Definition.ClrAssemblyName!).Distinct(StringComparer.Ordinal);
        _assemblies = assemblies?.ToArray() ?? referencedAssemblies.Select(name => new CatalogAssembly(name)).ToArray();
        if (_tables.Any(table => table is null)) throw new ArgumentException("Tables cannot contain null records.", nameof(tables));
        if (_indexes.Any(index => index is null)) throw new ArgumentException("Indexes cannot contain null records.", nameof(indexes));
        if (_scalarTypes.Any(type => type is null)) throw new ArgumentException("Scalar types cannot contain null records.", nameof(scalarTypes));
        if (_tableTypes.Any(type => type is null)) throw new ArgumentException("Table types cannot contain null records.", nameof(tableTypes));
        CatalogTable.ValidateUnique(_xmlSchemaCollections.Select(collection => (collection.SchemaName, collection.Name)),
            "XML schema collection names", nameof(xmlSchemaCollections));
        CatalogTable.ValidateUnique(_assemblies.Select(assembly => assembly.Name), "Assembly names", nameof(assemblies), StringComparer.Ordinal);
        if (_tables.Any(table => table.ObjectId == Guid.Empty)) throw new ArgumentException("Object identities must be nonempty.", nameof(tables));
        CatalogTable.ValidateUnique(_tables.Select(table => table.ObjectId), "Security object identities", nameof(tables));
        CatalogTable.ValidateUnique(_tables.Select(table => table.Id), "Table IDs", nameof(tables));
        CatalogTable.ValidateUnique(_tables.Select(table => (table.DatabaseName, table.SchemaName, table.Name)),
            "Qualified table names", nameof(tables));
        CatalogTable.ValidateUnique(_indexes.Select(index => index.Id), "Index IDs", nameof(indexes));
        var typeNames = _scalarTypes.Select(type => (type.SchemaName, type.Name))
            .Concat(_tableTypes.Select(type => (type.SchemaName, type.Name))).ToArray();
        CatalogTable.ValidateUnique(typeNames, "User-defined type names", nameof(scalarTypes));
        CatalogTable.ValidateUnique(_scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(type => (type.Definition.ClrAssemblyName!, type.Definition.ClrClassName!)),
            "CLR assembly/class bindings", nameof(scalarTypes));
        var xmlByName = _xmlSchemaCollections.ToDictionary(collection => (collection.SchemaName, collection.Name));
        foreach (var collection in referencedXml)
            if (!xmlByName.TryGetValue((collection.SchemaName, collection.Name), out var declared) || !declared.Equals(collection))
                throw new ArgumentException($"Typed XML references undeclared or mismatched collection {collection.QualifiedName}.", nameof(xmlSchemaCollections));
        var assemblyNames = _assemblies.Select(assembly => assembly.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var scalar in _scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr))
            if (!assemblyNames.Contains(scalar.Definition.ClrAssemblyName!))
                throw new ArgumentException($"CLR type {scalar.QualifiedName} references an undeclared assembly.", nameof(assemblies));

        var scalarTypesByName = _scalarTypes.ToDictionary(type => (type.SchemaName, type.Name));
        foreach (var column in _tables.SelectMany(table => table.Columns)
                     .Concat(_tableTypes.SelectMany(type => type.Columns)))
        {
            if (!column.Type.IsUserDefined) continue;
            var name = (column.Type.UserTypeSchema!, column.Type.UserTypeName!);
            if (!scalarTypesByName.TryGetValue(name, out var declared) || declared.Definition != column.Type)
                throw new ArgumentException($"Column '{column.Name}' references an undeclared or mismatched user-defined type.", nameof(scalarTypes));
        }

        var tablesById = _tables.ToDictionary(table => table.Id);
        var indexesById = _indexes.ToDictionary(index => index.Id);
        foreach (var table in _tables.Where(table => table.Graph is not null))
        {
            var graph = table.Graph!;
            if (table.SystemVersioning is not null)
                throw new ArgumentException("Graph tables cannot also be system-versioned.", nameof(tables));
            var identity = RequireGraphColumn(table, graph.IdentityColumnId, CatalogGeneratedAlwaysKind.GraphIdentity,
                nameof(tables));
            if ((graph.Kind == GraphTableKind.Node && table.Columns[^1].Id != identity.Id) ||
                (graph.Kind == GraphTableKind.Edge && table.Columns[^3].Id != identity.Id))
                throw new ArgumentException("Graph identity columns must occupy the documented trailing position.", nameof(tables));
            RequireGraphIndex(table, graph.IdentityIndexId, identity.Id, unique: true, indexesById, nameof(indexes));
            if (graph.Kind == GraphTableKind.Node) continue;

            if (!tablesById.TryGetValue(graph.FromNodeTableId!.Value, out var fromTable) ||
                fromTable.Graph?.Kind != GraphTableKind.Node ||
                !tablesById.TryGetValue(graph.ToNodeTableId!.Value, out var toTable) ||
                toTable.Graph?.Kind != GraphTableKind.Node)
                throw new ArgumentException("Graph edge endpoints must reference registered node tables.", nameof(tables));
            var from = RequireGraphColumn(table, graph.FromNodeColumnId!.Value,
                CatalogGeneratedAlwaysKind.GraphFromNode, nameof(tables));
            var to = RequireGraphColumn(table, graph.ToNodeColumnId!.Value,
                CatalogGeneratedAlwaysKind.GraphToNode, nameof(tables));
            if (table.Columns[^2].Id != from.Id || table.Columns[^1].Id != to.Id)
                throw new ArgumentException("Graph edge endpoint columns must be the final two columns.", nameof(tables));
            RequireGraphIndex(table, graph.OutgoingIndexId!.Value, from.Id, unique: false, indexesById,
                nameof(indexes));
            RequireGraphIndex(table, graph.IncomingIndexId!.Value, to.Id, unique: false, indexesById,
                nameof(indexes));
        }
        foreach (var current in _tables.Where(table => table.SystemVersioning is not null))
        {
            var temporal = current.SystemVersioning!;
            if (!tablesById.TryGetValue(temporal.HistoryTableId, out var history) || history.Id == current.Id)
                throw new ArgumentException($"System-versioned table '{current.Name}' references an invalid history table.", nameof(tables));
            if (history.SystemVersioning is not null)
                throw new ArgumentException("A temporal history table cannot itself be system-versioned.", nameof(tables));
            if (_tables.Count(table => table.SystemVersioning?.HistoryTableId == history.Id) != 1)
                throw new ArgumentException("A temporal history table must belong to exactly one current table.", nameof(tables));
            if (current.Columns.Count != history.Columns.Count || current.Columns.Zip(history.Columns).Any(pair =>
                    pair.First.Id != pair.Second.Id || pair.First.Name != pair.Second.Name ||
                    pair.First.Type != pair.Second.Type || pair.First.IsNullable != pair.Second.IsNullable))
                throw new ArgumentException("A temporal history table must have the current table's ordered column schema.", nameof(tables));
        }
        foreach (var group in _indexes.GroupBy(index => index.TableId))
        {
            CatalogTable.ValidateUnique(group.Select(index => index.Name), "Index names", nameof(indexes), StringComparer.Ordinal);
            if (group.Count(index => index.StorageKind == CatalogIndexStorageKind.Clustered) > 1)
                throw new ArgumentException("A disk table can have only one clustered index.", nameof(indexes));
            if (group.Count(index => index.StorageKind == CatalogIndexStorageKind.NonClustered) > 999)
                throw new ArgumentException("A disk table can have at most 999 nonclustered indexes.", nameof(indexes));
            if (group.Count(index => index.IsPrimaryKey) > 1)
                throw new ArgumentException("A table can have only one primary key.", nameof(indexes));
        }
        foreach (var table in _tables.Where(table => table.Columns.Any(column => column.IsFileStream)))
        {
            var rowGuid = table.Columns.Single(column => column.IsRowGuidCol);
            if (!_indexes.Any(index => index.TableId == table.Id && index.Method == CatalogIndexMethod.BTree &&
                                      index.IsUnique && index.Columns.Count == 1 &&
                                      index.Columns[0].ColumnId == rowGuid.Id))
                throw new ArgumentException("FILESTREAM requires a single-column UNIQUE or PRIMARY KEY index on the ROWGUIDCOL column.", nameof(indexes));
        }
        foreach (var index in _indexes)
        {
            if (!tablesById.TryGetValue(index.TableId, out var table))
                throw new ArgumentException($"Index '{index.Name}' references an unknown table.", nameof(indexes));
            var columnIds = table.Columns.Select(column => column.Id).ToHashSet();
            if (index.Columns.Any(column => !columnIds.Contains(column.ColumnId)))
                throw new ArgumentException($"Index '{index.Name}' references an unknown column.", nameof(indexes));
            if (index.IncludedColumns.Any(column => !columnIds.Contains(column)))
                throw new ArgumentException($"Index '{index.Name}' includes an unknown column.", nameof(indexes));
            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Count > 32)
                throw new ArgumentException("A rowstore index key can contain at most 32 columns.", nameof(indexes));
            if (index.IncludedColumns.Count > 1_023)
                throw new ArgumentException("A nonclustered index can contain at most 1,023 included columns.", nameof(indexes));
            foreach (var includedId in index.IncludedColumns)
            {
                var included = table.Columns.Single(column => column.Id == includedId);
                if (included.Type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image || included.IsColumnSet)
                    throw new ArgumentException($"Column '{included.Name}' cannot be included in an index.", nameof(indexes));
            }
            foreach (var indexedColumn in index.Columns)
            {
                var column = table.Columns.Single(candidate => candidate.Id == indexedColumn.ColumnId);
                if (column.IsColumnSet)
                    throw new ArgumentException("An XML column set cannot be indexed.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.Json)
                    throw new ArgumentException("A JSON index cannot be created on a computed JSON column.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.Xml &&
                    index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Primary or CatalogXmlIndexKind.Selective)
                    throw new ArgumentException("A primary or selective XML index cannot be created on a computed XML column.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.FullText)
                    throw new ArgumentException("A full-text index cannot be created on a computed column.", nameof(indexes));
                var compatible = index.Method switch
                {
                    CatalogIndexMethod.BTree => column.Type.CanBeBTreeKey,
                    CatalogIndexMethod.Json => column.Type.Name == SqlTypeName.Json,
                    CatalogIndexMethod.Spatial => column.Type.Name is SqlTypeName.Geometry or SqlTypeName.Geography,
                    CatalogIndexMethod.Vector => column.Type.Name == SqlTypeName.Vector,
                    CatalogIndexMethod.Xml => column.Type.Name == SqlTypeName.Xml,
                    CatalogIndexMethod.FullText => column.Type.Name is SqlTypeName.Char or SqlTypeName.VarChar or
                        SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText,
                    _ => false
                };
                if (!compatible)
                    throw new ArgumentException($"SQL Server type {column.Type} cannot be a B-tree key.", nameof(indexes));
                if (column.Encryption?.EncryptionType == CatalogEncryptionType.Randomized)
                    throw new ArgumentException("Randomized encrypted columns cannot be index keys.", nameof(indexes));
                if (column.IsSparse && (index.StorageKind == CatalogIndexStorageKind.Clustered || index.IsPrimaryKey))
                    throw new ArgumentException("A SPARSE column cannot be part of a clustered index or primary key.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.FullText &&
                    !StringComparer.OrdinalIgnoreCase.Equals(column.Type.CollationMetadata?.Name,
                        index.SpecializedOptions!.FullTextOptions!.Collation))
                    throw new ArgumentException(
                        $"Full-text index '{index.Name}' collation must exactly match its source column collation.",
                        nameof(indexes));
            }
            var maximumKeyBytes = index.StorageKind == CatalogIndexStorageKind.Clustered
                ? CatalogIndexKey.MaximumClusteredKeyBytes : CatalogIndexKey.MaximumNonClusteredKeyBytes;
            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Sum(indexedColumn =>
                    MinimumIndexKeyBytes(table.Columns.Single(column => column.Id == indexedColumn.ColumnId).Type)) >
                maximumKeyBytes)
                throw new ArgumentException("The fixed-width portion of the composite index key exceeds the SQL Server index-kind limit.", nameof(indexes));
            if (index.IsPrimaryKey && index.Columns.Any(indexed => table.Columns.Single(column => column.Id == indexed.ColumnId).IsNullable))
                throw new ArgumentException("Primary-key columns cannot be nullable.", nameof(indexes));
            if (index.Method is CatalogIndexMethod.Json or CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector or
                CatalogIndexMethod.Xml or CatalogIndexMethod.FullText)
            {
                var clusteringKey = _indexes.SingleOrDefault(candidate => candidate.TableId == table.Id &&
                    candidate.IsPrimaryKey && candidate.StorageKind == CatalogIndexStorageKind.Clustered);
                if (clusteringKey is null)
                    throw new ArgumentException("Specialized indexes require a clustered primary key.", nameof(indexes));
                var clusteringKeyBytes = clusteringKey.Columns.Sum(key =>
                    MaximumIndexKeyBytes(table.Columns.Single(column => column.Id == key.ColumnId).Type));
                if (index.Method == CatalogIndexMethod.Json &&
                    (clusteringKey.Columns.Count > 31 || clusteringKeyBytes >= 128))
                    throw new ArgumentException("A JSON index requires at most 31 clustering-key columns totaling less than 128 bytes.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.Spatial &&
                    (clusteringKey.Columns.Count > 15 || clusteringKeyBytes > 895))
                    throw new ArgumentException("A spatial index requires at most 15 primary-key columns totaling at most 895 bytes.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.Xml && clusteringKey.Columns.Count > 15)
                    throw new ArgumentException("An XML index requires at most 15 clustering-key columns.", nameof(indexes));
            }
        }
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Json).GroupBy(index => index.TableId))
            if (group.Count() > 249)
                throw new ArgumentException("A table can have at most 249 JSON indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Xml).GroupBy(index => index.TableId))
            if (group.Count() > 249)
                throw new ArgumentException("A table can have at most 249 XML indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.FullText)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 1)
                throw new ArgumentException("A textual column can have only one full-text index.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Spatial)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 249)
                throw new ArgumentException("A spatial column can have at most 249 spatial indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Json)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 1)
                throw new ArgumentException("A JSON column can have only one JSON index.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Xml)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
        {
            if (group.Count(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary) > 1)
                throw new ArgumentException("An XML column can have only one primary XML index.", nameof(indexes));
            if (group.Count(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective) > 1)
                throw new ArgumentException("An XML column can have only one selective XML index.", nameof(indexes));
            if (group.Any(index => index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Path or
                    CatalogXmlIndexKind.Value or CatalogXmlIndexKind.Property) &&
                !group.Any(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary))
                throw new ArgumentException("Secondary PATH, VALUE, and PROPERTY XML indexes require a primary XML index.", nameof(indexes));
        }
    }

    public IReadOnlyList<CatalogTable> Tables => Array.AsReadOnly(_tables);
    public IReadOnlyList<CatalogIndex> Indexes => Array.AsReadOnly(_indexes);
    public IReadOnlyList<CatalogScalarType> ScalarTypes => Array.AsReadOnly(_scalarTypes);
    public IReadOnlyList<CatalogTableType> TableTypes => Array.AsReadOnly(_tableTypes);
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections => Array.AsReadOnly(_xmlSchemaCollections);
    public IReadOnlyList<CatalogAssembly> Assemblies => Array.AsReadOnly(_assemblies);

    private static CatalogColumn RequireGraphColumn(CatalogTable table, ColumnId columnId,
        CatalogGeneratedAlwaysKind generatedAlways, string parameterName)
    {
        var column = table.Columns.SingleOrDefault(candidate => candidate.Id == columnId) ??
            throw new ArgumentException("Graph metadata references an unknown column.", parameterName);
        if (column.Type != SqlType.Binary(GraphNodeId.EncodedLength) || column.IsNullable || !column.IsHidden ||
            column.GeneratedAlways != generatedAlways || column.DefaultExpression is not null ||
            column.Identity is not null || column.IsComputed || column.Encryption is not null)
            throw new ArgumentException(
                $"Graph column '{column.Name}' must be hidden, non-null binary({GraphNodeId.EncodedLength}) with its exact generated-always kind.",
                parameterName);
        return column;
    }

    private static void RequireGraphIndex(CatalogTable table, IndexId indexId, ColumnId columnId, bool unique,
        IReadOnlyDictionary<IndexId, CatalogIndex> indexes, string parameterName)
    {
        if (!indexes.TryGetValue(indexId, out var index) || index.TableId != table.Id ||
            index.Method != CatalogIndexMethod.BTree || index.IsUnique != unique || index.Columns.Count != 1 ||
            index.Columns[0].ColumnId != columnId)
            throw new ArgumentException("Graph metadata references an incompatible identity or adjacency index.",
                parameterName);
    }

    internal static int MaximumIndexKeyBytes(SqlType type) => type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MaximumIndexKeyBytes(type.BaseType!),
        SqlUserTypeKind.Clr when type.ClrMaxByteSize is null => CatalogIndexKey.MaximumNonClusteredKeyBytes,
        SqlUserTypeKind.Clr => type.ClrMaxByteSize is > 0 and <= CatalogIndexKey.MaximumNonClusteredKeyBytes
            ? type.ClrMaxByteSize.Value : CatalogIndexKey.MaximumNonClusteredKeyBytes + 1,
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
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 :
                type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => type.Length!.Value,
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(type.Length!.Value * 2),
            SqlTypeName.HierarchyId => 892,
            SqlTypeName.SqlVariant => CatalogIndexKey.MaximumSqlVariantKeyBytes,
            _ => CatalogIndexKey.MaximumNonClusteredKeyBytes + 1
        }
    };

    internal static int MinimumIndexKeyBytes(SqlType type) => type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MinimumIndexKeyBytes(type.BaseType!),
        SqlUserTypeKind.Clr => type.ClrIsFixedLength && type.ClrMaxByteSize is > 0 ? type.ClrMaxByteSize.Value : 0,
        _ => type.Name switch
        {
            SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary or SqlTypeName.HierarchyId or
                SqlTypeName.SqlVariant => 0,
            _ => MaximumIndexKeyBytes(type)
        }
    };
}

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.CatalogCodecs;
/// <summary>Encodes the self-describing bootstrap catalog without relying on user schemas.</summary>
internal sealed partial class CatalogCodecManagementService : ICatalogCodecManagementService
{
    private readonly sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogTableSchemasCoordinationService catalogTableSchemas;
    private readonly sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogDefinitionTransportCoordinationService catalogDefinitionTransport;
    private readonly sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogIndexSchemasCoordinationService catalogIndexSchemas;
    public CatalogCodecManagementService(sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogTableSchemasCoordinationService catalogTableSchemas, sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogDefinitionTransportCoordinationService catalogDefinitionTransport, sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogIndexSchemasCoordinationService catalogIndexSchemas)
    {
        this.catalogTableSchemas = catalogTableSchemas;
        this.catalogDefinitionTransport = catalogDefinitionTransport;
        this.catalogIndexSchemas = catalogIndexSchemas;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 27:
                catalogCodecContext.Payload = EncodeCatalogDefinition(catalogDefinition: catalogCodecContext.CatalogDefinition!);
                return catalogCodecContext;
            case 28:
                catalogCodecContext.CatalogDefinition = DecodeCatalogDefinition(source: catalogCodecContext.Payload!);
                return catalogCodecContext;
            case 9:
            case 10:
            case 11:
            case 12:
            case 16:
            case 17:
            case 19:
            case 20:
            case 21:
                return catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 7:
            case 8:
            case 13:
            case 22:
            case 23:
            case 24:
                return catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            default:
                return catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
        }
    });

    private byte[] EncodeCatalogDefinition(CatalogDefinition catalogDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateCatalog(catalog: catalogDefinition);
        return EncodeCore(catalog: catalogDefinition);
    });

    private CatalogDefinition DecodeCatalogDefinition(byte[] source) =>
        TryCatch(operation: () =>
    {
        ValidateSource(source: source);
        return DecodeCore(source: source);
    });
    private const ushort FormatVersion = 13;
    private const uint Magic = 0x33544143;
    private const uint Version12Magic = 0x32544143;
    private const uint Version11Magic = 0x31544143;
    private const uint Version10Magic = 0x30544143;
    private const uint Version9Magic = 0x39544143;
    private const uint Version8Magic = 0x38544143;
    private const uint Version7Magic = 0x37544143;
    private const uint Version6Magic = 0x36544143;
    private const int MaximumRecordCount = 65_535;
    private const int MaximumStringBytes = int.MaxValue;
    private byte[] EncodeCore(CatalogDefinition catalog)
    {
        ArgumentNullException.ThrowIfNull(argument: catalog);
        ValidateCount(count: catalog.Tables.Count, parameterName: nameof(catalog));
        ValidateCount(count: catalog.Indexes.Count, parameterName: nameof(catalog));
        ValidateCount(count: catalog.ScalarTypes.Count, parameterName: nameof(catalog));
        ValidateCount(count: catalog.TableTypes.Count, parameterName: nameof(catalog));
        ValidateCount(count: catalog.XmlSchemaCollections.Count, parameterName: nameof(catalog));
        ValidateCount(count: catalog.Assemblies.Count, parameterName: nameof(catalog));
        var output = new ArrayBufferWriter<byte>();
        WriteUInt32(output: output, value: Magic);
        WriteUInt16(output: output, value: FormatVersion);
        WriteUInt16(output: output, value: 0);
        WriteUInt32(output: output, value: checked((uint)catalog.Tables.Count));
        WriteUInt32(output: output, value: checked((uint)catalog.Indexes.Count));
        WriteUInt32(output: output, value: checked((uint)catalog.ScalarTypes.Count));
        WriteUInt32(output: output, value: checked((uint)catalog.TableTypes.Count));
        WriteUInt32(output: output, value: checked((uint)catalog.XmlSchemaCollections.Count));
        WriteUInt32(output: output, value: checked((uint)catalog.Assemblies.Count));

        foreach (var collection in catalog.XmlSchemaCollections)
        {
            WriteString(output: output, value: collection.SchemaName);
            WriteString(output: output, value: collection.Name);

            var definitions = ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: collection);
            WriteUInt32(output: output, value: checked((uint)definitions.Count));

            foreach (var definition in definitions)
            {
                WriteString(output: output, value: definition);
            }
        }

        foreach (var assembly in catalog.Assemblies)
        {
            WriteString(output: output, value: assembly.Name);
            WriteString(output: output, value: assembly.Version ?? string.Empty);
            WriteString(output: output, value: assembly.Culture ?? string.Empty);
            WriteString(output: output, value: assembly.PublicKeyToken ?? string.Empty);
            WriteByte(output: output, value: (byte)assembly.PermissionSet);

            WriteBytes(output: output, value: ReadCatalogAssemblyImage(catalogAssembly: assembly)
            .Span);
        }

        foreach (var table in catalog.Tables)
        {
            WriteUInt64(output: output, value: table.Id.Value);
            WriteBytes(output: output, value: table.ObjectId.ToByteArray());
            WriteString(output: output, value: table.DatabaseName);
            WriteString(output: output, value: table.SchemaName);
            WriteString(output: output, value: table.Name);
            WriteUInt64(output: output, value: table.SchemaVersion);
            WriteUInt64(output: output, value: table.FirstHeapPageId.Value);
            WriteUInt32(output: output, value: checked((uint)table.Columns.Count));

            foreach (var column in table.Columns)
            {
                WriteColumn(output: output, column: column);
            }

            WriteUInt32(output: output, value: checked((uint)table.CheckConstraints.Count));

            foreach (var check in table.CheckConstraints)
            {
                WriteString(output: output, value: check.Name);
                WriteString(output: output, value: check.Expression);
            }

            WriteByte(output: output, value: table.NextIdentityValue is null ? (byte)0 : (byte)1);

            if (table.NextIdentityValue is { } nextIdentity)
            {
                WriteBigInteger(output: output, value: nextIdentity);
            }

            WriteByte(output: output, value: table.SystemVersioning is null ? (byte)0 : (byte)1);

            if (table.SystemVersioning is { } temporal)
            {
                WriteUInt64(output: output, value: temporal.HistoryTableId.Value);
                WriteUInt64(output: output, value: temporal.PeriodStartColumnId.Value);
                WriteUInt64(output: output, value: temporal.PeriodEndColumnId.Value);
            }

            WriteByte(output: output, value: table.Graph is null ? (byte)0 : (byte)1);

            if (table.Graph is { } graph)
            {
                WriteByte(output: output, value: (byte)graph.Kind);
                WriteUInt64(output: output, value: graph.IdentityColumnId.Value);
                WriteUInt64(output: output, value: graph.IdentityIndexId.Value);

                if (graph.Kind == GraphTableKind.Edge)
                {
                    WriteUInt64(output: output, value: graph.FromNodeTableId!.Value.Value);
                    WriteUInt64(output: output, value: graph.FromNodeColumnId!.Value.Value);
                    WriteUInt64(output: output, value: graph.OutgoingIndexId!.Value.Value);
                    WriteUInt64(output: output, value: graph.ToNodeTableId!.Value.Value);
                    WriteUInt64(output: output, value: graph.ToNodeColumnId!.Value.Value);
                    WriteUInt64(output: output, value: graph.IncomingIndexId!.Value.Value);
                }
            }
        }

        foreach (var index in catalog.Indexes)
        {
            WriteUInt64(output: output, value: index.Id.Value);
            WriteString(output: output, value: index.Name);
            WriteUInt64(output: output, value: index.TableId.Value);
            WriteUInt64(output: output, value: index.RootPageId.Value);
            WriteByte(output: output, value: index.IsUnique ? (byte)1 : (byte)0);
            WriteByte(output: output, value: (byte)index.Method);
            WriteUInt16(output: output, value: checked((ushort)index.Columns.Count));

            foreach (var column in index.Columns)
            {
                WriteUInt64(output: output, value: column.ColumnId.Value);
                WriteByte(output: output, value: (byte)column.Direction);
                WriteByte(output: output, value: (byte)column.NullSortOrder);
                WriteString(output: output, value: column.Collation ?? string.Empty);
            }

            var specializedValues = index.SpecializedOptions?.FullTextOptions is { } fullTextOptions ? EncodeCatalogFullTextIndexOptions(catalogFullTextIndexOptions: fullTextOptions) : index.SpecializedOptions?.JsonPaths ?? [];
            WriteUInt16(output: output, value: checked((ushort)specializedValues.Count));

            foreach (var value in specializedValues)
            {
                WriteString(output: output, value: value);
            }

            WriteByte(output: output, value: index.SpecializedOptions?.VectorMetric is null ? (byte)0 : (byte)index.SpecializedOptions.VectorMetric.Value);

            if (index.Method == CatalogIndexMethod.Spatial)
            {
                WriteByte(output: output, value: index.SpecializedOptions?.SpatialSrid is null ? (byte)0 : (byte)1);

                if (index.SpecializedOptions?.SpatialSrid is { } spatialSrid)
                {
                    WriteInt32(output: output, value: spatialSrid);
                }
            }

            WriteByte(output: output, value: index.SpecializedOptions?.XmlIndexKind is null ? (byte)0 : (byte)index.SpecializedOptions.XmlIndexKind.Value);
            WriteUInt16(output: output, value: checked((ushort)(index.SpecializedOptions?.XmlNamespaces.Count ?? 0)));

            foreach (var binding in (index.SpecializedOptions?.XmlNamespaces ?? new Dictionary<string, string>()).OrderBy(keySelector: item => item.Key, comparer: StringComparer.Ordinal))
            {
                WriteString(output: output, value: binding.Key);
                WriteString(output: output, value: binding.Value);
            }

            WriteByte(output: output, value: (byte)index.StorageKind);
            WriteByte(output: output, value: index.IsPrimaryKey ? (byte)1 : (byte)0);
            WriteByte(output: output, value: index.IgnoreDuplicateKey ? (byte)1 : (byte)0);
            WriteByte(output: output, value: 0);
            WriteUInt16(output: output, value: checked((ushort)index.IncludedColumns.Count));

            foreach (var included in index.IncludedColumns)
            {
                WriteUInt64(output: output, value: included.Value);
            }
        }

        foreach (var scalarType in catalog.ScalarTypes)
        {
            WriteType(output: output, type: scalarType.Definition);
        }

        foreach (var tableType in catalog.TableTypes)
        {
            WriteString(output: output, value: tableType.SchemaName);
            WriteString(output: output, value: tableType.Name);
            WriteByte(output: output, value: tableType.IsMemoryOptimized ? (byte)1 : (byte)0);
            WriteByte(output: output, value: 0);
            WriteUInt16(output: output, value: 0);
            WriteUInt32(output: output, value: checked((uint)tableType.Columns.Count));

            foreach (var column in tableType.Columns)
            {
                WriteColumn(output: output, column: column);
            }

            WriteUInt32(output: output, value: checked((uint)tableType.Indexes.Count));

            foreach (var index in tableType.Indexes)
            {
                WriteString(output: output, value: index.Name);
                WriteByte(output: output, value: index.IsPrimaryKey ? (byte)1 : (byte)0);
                WriteByte(output: output, value: index.IsUnique ? (byte)1 : (byte)0);
                WriteByte(output: output, value: (byte)index.StorageKind);
                WriteByte(output: output, value: index.IgnoreDuplicateKey ? (byte)1 : (byte)0);
                WriteUInt16(output: output, value: checked((ushort)index.Columns.Count));

                foreach (var column in index.Columns)
                {
                    WriteIndexedColumn(output: output, column: column);
                }

                WriteUInt16(output: output, value: checked((ushort)index.IncludedColumns.Count));

                foreach (var columnId in index.IncludedColumns)
                {
                    WriteUInt64(output: output, value: columnId.Value);
                }

                WriteUInt64(output: output, value: checked((ulong)(index.BucketCount ?? 0)));
            }

            WriteUInt32(output: output, value: checked((uint)tableType.CheckConstraints.Count));

            foreach (var check in tableType.CheckConstraints)
            {
                WriteString(output: output, value: check.Name);
                WriteString(output: output, value: check.Expression);
            }
        }

        WriteBytes(output: output, value: catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 1, SecurityStatePayload = new sql_storage_engine.Models.Security.SecurityStatePayload { State = catalog.Security } })
            .EncodeSecurityStatePayloadResult!);

        return output.WrittenSpan.ToArray();
    }

    private Guid ReadObjectId(byte[] bytes)
    {
        if (bytes.Length != 16 || new Guid(b: bytes) == Guid.Empty)
        {
            throw new StorageFormatException(message: "Invalid security object identity.");
        }

        return new Guid(b: bytes);
    }

    private CatalogDefinition DecodeCore(ReadOnlySpan<byte> source)
    {
        var reader = new ReadableCatalogRecordContext
        {
            Source = source
        };

        var magic = ReadCatalogUInt32(reader: ref reader);

        if (magic is not (Magic or Version12Magic or Version11Magic or Version10Magic or Version9Magic or Version8Magic or Version7Magic or Version6Magic))
        {
            throw new StorageFormatException(message: "Invalid bootstrap catalog magic number.");
        }

        var version = ReadCatalogUInt16(reader: ref reader);

        if (version is not (6 or 7 or 8 or 9 or 10 or 11 or 12 or FormatVersion) || version == 6 && magic != Version6Magic || version == 7 && magic != Version7Magic || version == 8 && magic != Version8Magic || version == 9 && magic != Version9Magic || version == 10 && magic != Version10Magic || version == 11 && magic != Version11Magic || version == 12 && magic != Version12Magic || version == FormatVersion && magic != Magic)
        {
            throw new StorageFormatException(message: $"Unsupported catalog format version {version}.");
        }

        ReadCatalogSetVersion(reader: ref reader, version: version);

        if (ReadCatalogUInt16(reader: ref reader) != 0)
        {
            throw new StorageFormatException(message: "Reserved catalog header bytes must be zero.");
        }

        var tableCount = ReadCatalogCount(reader: ref reader);
        var indexCount = ReadCatalogCount(reader: ref reader);
        var scalarTypeCount = ReadCatalogCount(reader: ref reader);
        var tableTypeCount = ReadCatalogCount(reader: ref reader);
        var xmlSchemaCount = ReadCatalogCount(reader: ref reader);
        var assemblyCount = ReadCatalogCount(reader: ref reader);
        List<SqlXmlSchemaCollection> xmlSchemas = new(capacity: xmlSchemaCount);

        for (var schemaNumber = 0; schemaNumber < xmlSchemaCount; schemaNumber++)
        {
            var schemaName = ReadCatalogString(reader: ref reader);
            var name = ReadCatalogString(reader: ref reader);
            var definitionCount = ReadCatalogCount(reader: ref reader, nonZero: true);
            var definitions = new string[definitionCount];

            for (var index = 0; index < definitions.Length; index++)
            {
                definitions[index] = ReadCatalogString(reader: ref reader);
            }

            try
            {
                xmlSchemas.Add(item: CreateSqlXmlSchemaCollection(schemaName: schemaName, name: name, definitions: definitions));
            }
            catch (Exception exception)when (exception is ArgumentException or XmlException or XmlSchemaException)
            {
                throw new StorageFormatException(message: "Invalid XML schema collection record.", innerException: exception);
            }
        }

        ReadCatalogSetXmlSchemas(reader: ref reader, schemas: xmlSchemas);
        List<CatalogAssembly> assemblies = new(capacity: assemblyCount);

        for (var assemblyNumber = 0; assemblyNumber < assemblyCount; assemblyNumber++)
        {
            var name = ReadCatalogString(reader: ref reader);
            var versionText = ReadCatalogString(reader: ref reader);
            var culture = ReadCatalogString(reader: ref reader);
            var token = ReadCatalogString(reader: ref reader);
            var permission = (CatalogAssemblyPermissionSet)ReadCatalogByte(reader: ref reader);
            var image = ReadCatalogBytes(reader: ref reader);

            try
            {
                assemblies.Add(item: CreateCatalogAssembly(name: name, image: image, version: EmptyToNull(value: versionText), culture: EmptyToNull(value: culture), publicKeyToken: EmptyToNull(value: token), permissionSet: permission));
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException(message: "Invalid CLR assembly record.", innerException: exception);
            }
        }

        List<CatalogTable> tables = new(capacity: tableCount);

        for (var tableNumber = 0; tableNumber < tableCount; tableNumber++)
        {
            var id = new sql_storage_engine.Models.Identifiers.TableId
            {
                Value = ReadCatalogUInt64(reader: ref reader)
            };

            var objectId = version >= 11 ? ReadObjectId(bytes: ReadCatalogBytes(reader: ref reader)) : Guid.NewGuid();
            var databaseName = version >= 7 ? ReadCatalogString(reader: ref reader) : "default";
            var schemaName = version >= 7 ? ReadCatalogString(reader: ref reader) : "dbo";
            var name = ReadCatalogString(reader: ref reader);
            var schemaVersion = ReadCatalogUInt64(reader: ref reader);

            var heapRoot = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = ReadCatalogUInt64(reader: ref reader)
            };

            var columnCount = ReadCatalogCount(reader: ref reader, nonZero: true);
            List<CatalogColumn> columns = new(capacity: columnCount);

            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                columns.Add(item: ReadCatalogColumn(reader: ref reader));
            }

            var checkCount = ReadCatalogCount(reader: ref reader);
            var checks = new CatalogCheckConstraint[checkCount];

            for (var checkNumber = 0; checkNumber < checkCount; checkNumber++)
            {
                checks[checkNumber] = CreateCatalogCheckConstraint(name: ReadCatalogString(reader: ref reader), expression: ReadCatalogString(reader: ref reader));
            }

            var nextIdentity = ReadCatalogBoolean(reader: ref reader) ? ReadCatalogBigInteger(reader: ref reader) : (BigInteger? )null;
            CatalogSystemVersioning? systemVersioning = null;

            if (version >= 8 && ReadCatalogBoolean(reader: ref reader))
            {
                var historyTableId = new sql_storage_engine.Models.Identifiers.TableId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                var periodStartColumnId = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                var periodEndColumnId = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                try
                {
                    systemVersioning = CreateCatalogSystemVersioning(tableId: historyTableId, periodStartColumnId: (periodStartColumnId).Value, periodEndColumnId: (periodEndColumnId).Value);
                }
                catch (ArgumentException exception)
                {
                    throw new StorageFormatException(message: "Invalid system-versioning metadata.", innerException: exception);
                }
            }

            CatalogGraphTable? graph = null;

            if (version >= 10 && ReadCatalogBoolean(reader: ref reader))
            {
                var kind = (GraphTableKind)ReadCatalogByte(reader: ref reader);

                var identityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                var identityIndexId = new sql_storage_engine.Models.Identifiers.IndexId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                try
                {
                    graph = kind switch
                    {
                        GraphTableKind.Node => CreateCatalogGraphTableNode(identityColumnId: (identityColumnId).Value, identityIndexId: (identityIndexId).Value),
                        GraphTableKind.Edge => CreateCatalogGraphTableEdge(identityColumnId: (identityColumnId).Value, identityIndexId: (identityIndexId).Value, fromNodeTableId: (new sql_storage_engine.Models.Identifiers.TableId { Value = ReadCatalogUInt64(reader: ref reader) }).Value, fromNodeColumnId: (new sql_storage_engine.Models.Identifiers.ColumnId { Value = ReadCatalogUInt64(reader: ref reader) }).Value, outgoingIndexId: (new sql_storage_engine.Models.Identifiers.IndexId { Value = ReadCatalogUInt64(reader: ref reader) }).Value, toNodeTableId: (new sql_storage_engine.Models.Identifiers.TableId { Value = ReadCatalogUInt64(reader: ref reader) }).Value, toNodeColumnId: (new sql_storage_engine.Models.Identifiers.ColumnId { Value = ReadCatalogUInt64(reader: ref reader) }).Value, incomingIndexId: (new sql_storage_engine.Models.Identifiers.IndexId { Value = ReadCatalogUInt64(reader: ref reader) }).Value),
                        _ => throw new ArgumentOutOfRangeException(paramName: nameof(kind))};
                }
                catch (ArgumentException exception)
                {
                    throw new StorageFormatException(message: "Invalid graph-table metadata.", innerException: exception);
                }
            }

            try
            {
                tables.Add(item: CreateCatalogTable(tableId: id, name: name, schemaVersion: schemaVersion, pageId: heapRoot, columns: columns, checkConstraints: checks, nextIdentityValue: nextIdentity, databaseName: databaseName, schemaName: schemaName, catalogSystemVersioning: systemVersioning, catalogGraphTable: graph)with { ObjectId = objectId });
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException(message: "Invalid catalog table record.", innerException: exception);
            }
        }

        List<CatalogIndex> indexes = new(capacity: indexCount);

        for (var indexNumber = 0; indexNumber < indexCount; indexNumber++)
        {
            var id = new sql_storage_engine.Models.Identifiers.IndexId
            {
                Value = ReadCatalogUInt64(reader: ref reader)
            };

            var name = ReadCatalogString(reader: ref reader);

            var tableId = new sql_storage_engine.Models.Identifiers.TableId
            {
                Value = ReadCatalogUInt64(reader: ref reader)
            };

            var root = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = ReadCatalogUInt64(reader: ref reader)
            };

            var unique = ReadCatalogBoolean(reader: ref reader);
            var method = (CatalogIndexMethod)ReadCatalogByte(reader: ref reader);

            if (!Enum.IsDefined(value: method))
            {
                throw new StorageFormatException(message: "Unknown catalog index method.");
            }

            var columnCount = ReadCatalogUInt16(reader: ref reader);

            if (columnCount == 0)
            {
                throw new StorageFormatException(message: "Catalog index has no columns.");
            }

            List<CatalogIndexedColumn> columns = new(capacity: columnCount);

            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                var columnId = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };

                var direction = (SortDirection)ReadCatalogByte(reader: ref reader);
                var nullOrder = (NullSortOrder)ReadCatalogByte(reader: ref reader);
                var collation = ReadCatalogString(reader: ref reader);

                try
                {
                    columns.Add(item: CreateCatalogIndexedColumn(columnId: columnId, direction: direction, nullSortOrder: nullOrder, collation: collation.Length == 0 ? null : collation));
                }
                catch (ArgumentException exception)
                {
                    throw new StorageFormatException(message: "Invalid indexed-column record.", innerException: exception);
                }
            }

            var specializedValueCount = ReadCatalogUInt16(reader: ref reader);
            var specializedValues = new string[specializedValueCount];

            for (var valueNumber = 0; valueNumber < specializedValues.Length; valueNumber++)
            {
                specializedValues[valueNumber] = ReadCatalogString(reader: ref reader);
            }

            var rawMetric = ReadCatalogByte(reader: ref reader);
            int? spatialSrid = null;

            if (version >= 9 && method == CatalogIndexMethod.Spatial)
            {
                spatialSrid = ReadCatalogByte(reader: ref reader) switch
                {
                    0 => null,
                    1 => ReadCatalogInt32(reader: ref reader),
                    _ => throw new StorageFormatException(message: "Invalid spatial-index SRID marker.")};
            }

            var rawXmlKind = ReadCatalogByte(reader: ref reader);
            var xmlNamespaceCount = ReadCatalogUInt16(reader: ref reader);
            var xmlNamespaces = new Dictionary<string, string>(capacity: xmlNamespaceCount, comparer: StringComparer.Ordinal);

            for (var namespaceNumber = 0; namespaceNumber < xmlNamespaceCount; namespaceNumber++)
            {
                if (!xmlNamespaces.TryAdd(key: ReadCatalogString(reader: ref reader), value: ReadCatalogString(reader: ref reader)))
                {
                    throw new StorageFormatException(message: "Duplicate XML namespace prefix in specialized-index options.");
                }
            }

            var storageKind = (CatalogIndexStorageKind)ReadCatalogByte(reader: ref reader);
            var primary = ReadCatalogBoolean(reader: ref reader);
            var ignoreDuplicate = ReadCatalogBoolean(reader: ref reader);

            if (ReadCatalogByte(reader: ref reader) != 0)
            {
                throw new StorageFormatException(message: "Reserved index-option byte must be zero.");
            }

            var includedCount = ReadCatalogUInt16(reader: ref reader);
            var included = new ColumnId[includedCount];

            for (var includedNumber = 0; includedNumber < included.Length; includedNumber++)
            {
                included[includedNumber] = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = ReadCatalogUInt64(reader: ref reader)
                };
            }

            CatalogSpecializedIndexOptions? options = method switch
            {
                CatalogIndexMethod.BTree when specializedValues.Length == 0 && rawMetric == 0 && spatialSrid is null && rawXmlKind == 0 && xmlNamespaces.Count == 0 => null,
                CatalogIndexMethod.Json when rawMetric == 0 && spatialSrid is null && rawXmlKind == 0 && xmlNamespaces.Count == 0 => CreateJsonCatalogSpecializedIndexOptions(paths: specializedValues),
                CatalogIndexMethod.Spatial when specializedValues.Length == 0 && rawMetric == 0 && rawXmlKind == 0 && xmlNamespaces.Count == 0 => spatialSrid is { } exactSrid ? CreateSpatialCatalogSpecializedIndexOptions(srid: exactSrid) : PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Spatial }),
                CatalogIndexMethod.Vector when specializedValues.Length == 0 && rawMetric != 0 && spatialSrid is null && rawXmlKind == 0 && xmlNamespaces.Count == 0 => CreateVectorCatalogSpecializedIndexOptions(metric: (SqlVectorDistanceMetric)rawMetric),
                CatalogIndexMethod.Xml when rawMetric == 0 && spatialSrid is null && rawXmlKind != 0 => CreateXmlCatalogSpecializedIndexOptions(kind: (CatalogXmlIndexKind)rawXmlKind, namespaces: xmlNamespaces, paths: specializedValues),
                CatalogIndexMethod.FullText when rawMetric == 0 && spatialSrid is null && rawXmlKind == 0 && xmlNamespaces.Count == 0 => DecodeFullTextOptions(values: specializedValues),
                _ => throw new StorageFormatException(message: "Invalid specialized-index options.")};

            try
            {
                indexes.Add(item: CreateCatalogIndex(indexId: id, name: name, tableId: tableId, pageId: root, isUnique: unique, columns: columns, catalogSpecializedIndexOptions: options, storageKind: storageKind, isPrimaryKey: primary, includedColumns: included, ignoreDuplicateKey: ignoreDuplicate));
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException(message: "Invalid catalog index record.", innerException: exception);
            }
        }

        List<CatalogScalarType> scalarTypes = new(capacity: scalarTypeCount);

        for (var typeNumber = 0; typeNumber < scalarTypeCount; typeNumber++)
        {
            try
            {
                scalarTypes.Add(item: CreateCatalogScalarType(sqlType: ReadCatalogType(reader: ref reader)));
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException(message: "Invalid scalar user-defined type record.", innerException: exception);
            }
        }

        List<CatalogTableType> tableTypes = new(capacity: tableTypeCount);

        for (var typeNumber = 0; typeNumber < tableTypeCount; typeNumber++)
        {
            var schemaName = ReadCatalogString(reader: ref reader);
            var name = ReadCatalogString(reader: ref reader);
            var memoryOptimized = ReadCatalogBoolean(reader: ref reader);

            if (ReadCatalogByte(reader: ref reader) != 0 || ReadCatalogUInt16(reader: ref reader) != 0)
            {
                throw new StorageFormatException(message: "Reserved table-type bytes must be zero.");
            }

            var columnCount = ReadCatalogCount(reader: ref reader, nonZero: true);
            List<CatalogColumn> columns = new(capacity: columnCount);

            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                columns.Add(item: ReadCatalogColumn(reader: ref reader));
            }

            var typeIndexCount = ReadCatalogCount(reader: ref reader);
            List<CatalogTableTypeIndex> typeIndexes = new(capacity: typeIndexCount);

            for (var indexNumber = 0; indexNumber < typeIndexCount; indexNumber++)
            {
                var indexName = ReadCatalogString(reader: ref reader);
                var primary = ReadCatalogBoolean(reader: ref reader);
                var unique = ReadCatalogBoolean(reader: ref reader);
                var storageKind = (CatalogIndexStorageKind)ReadCatalogByte(reader: ref reader);
                var ignoreDuplicateKey = ReadCatalogBoolean(reader: ref reader);
                var indexedColumnCount = ReadCatalogUInt16(reader: ref reader);

                if (indexedColumnCount == 0)
                {
                    throw new StorageFormatException(message: "Table-type index has no columns.");
                }

                List<CatalogIndexedColumn> indexedColumns = new(capacity: indexedColumnCount);

                for (var columnNumber = 0; columnNumber < indexedColumnCount; columnNumber++)
                {
                    indexedColumns.Add(item: ReadCatalogIndexedColumn(reader: ref reader));
                }

                var includedCount = ReadCatalogUInt16(reader: ref reader);
                var included = new ColumnId[includedCount];

                for (var includeNumber = 0; includeNumber < included.Length; includeNumber++)
                {
                    included[includeNumber] = new sql_storage_engine.Models.Identifiers.ColumnId
                    {
                        Value = ReadCatalogUInt64(reader: ref reader)
                    };
                }

                var rawBucketCount = ReadCatalogUInt64(reader: ref reader);
                long? bucketCount = rawBucketCount == 0 ? null : checked((long)rawBucketCount);

                try
                {
                    typeIndexes.Add(item: CreateCatalogTableTypeIndex(name: indexName, isPrimaryKey: primary, isUnique: unique, columns: indexedColumns, storageKind: storageKind, includedColumns: included, ignoreDuplicateKey: ignoreDuplicateKey, bucketCount: bucketCount));
                }
                catch (ArgumentException exception)
                {
                    throw new StorageFormatException(message: "Invalid table-type index record.", innerException: exception);
                }
            }

            var checkCount = ReadCatalogCount(reader: ref reader);
            List<CatalogCheckConstraint> checks = new(capacity: checkCount);

            for (var checkNumber = 0; checkNumber < checkCount; checkNumber++)
            {
                try
                {
                    checks.Add(item: CreateCatalogCheckConstraint(name: ReadCatalogString(reader: ref reader), expression: ReadCatalogString(reader: ref reader)));
                }
                catch (ArgumentException exception)
                {
                    throw new StorageFormatException(message: "Invalid table-type check constraint.", innerException: exception);
                }
            }

            try
            {
                tableTypes.Add(item: CreateCatalogTableType(schemaName: schemaName, name: name, columns: columns, indexes: typeIndexes, checkConstraints: checks, isMemoryOptimized: memoryOptimized));
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException(message: "Invalid table-type record.", innerException: exception);
            }
        }

        var security = version >= 11 ? catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 2, SecurityStatePayload = new sql_storage_engine.Models.Security.SecurityStatePayload { Bytes = ReadCatalogBytes(reader: ref reader) } })
            .DecodeSecurityStatePayloadResult!.State! : new sql_storage_engine.Models.Security.StorageSecurityState
        {
            Revision = 1,
            Principals = [],
            Permissions = [],
            Audit = [],
            RetiredPrincipals = [],
            DatabasePermissions = [],
            PrincipalDependencies = []
        };

        if (!(reader.Offset == reader.Source.Length))
        {
            throw new StorageFormatException(message: "Bootstrap catalog contains trailing bytes.");
        }

        try
        {
            return CreateCatalogDefinition(tables: tables, indexes: indexes, scalarTypes: scalarTypes, tableTypes: tableTypes, xmlSchemaCollections: xmlSchemas, assemblies: assemblies)with
            {
                Security = security
            };
        }
        catch (ArgumentException exception)
        {
            throw new StorageCorruptionException(message: "Bootstrap catalog cross-references are invalid.", innerException: exception);
        }
    }

    private void ValidateCount(int count, string parameterName)
    {
        if (count > MaximumRecordCount)
        {
            throw new ArgumentException(message: "Catalog record count exceeds the format limit.", paramName: parameterName);
        }
    }

    private CatalogSpecializedIndexOptions DecodeFullTextOptions(IReadOnlyList<string> values)
    {
        try
        {
            return CreateFullTextCatalogSpecializedIndexOptions(catalogFullTextIndexOptions: DecodeCatalogFullTextIndexOptions(values: values));
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Invalid full-text index metadata.", innerException: exception);
        }
    }

    private void WriteByte(IBufferWriter<byte> output, byte value)
    {
        var span = output.GetSpan(sizeHint: 1);
        span[0] = value;
        output.Advance(count: 1);
    }

    private void WriteInt32(IBufferWriter<byte> output, int value)
    {
        var span = output.GetSpan(sizeHint: 4);
        BinaryPrimitives.WriteInt32LittleEndian(destination: span, value: value);
        output.Advance(count: 4);
    }

    private void WriteUInt16(IBufferWriter<byte> output, ushort value)
    {
        var span = output.GetSpan(sizeHint: 2);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: span, value: value);
        output.Advance(count: 2);
    }

    private void WriteUInt32(IBufferWriter<byte> output, uint value)
    {
        var span = output.GetSpan(sizeHint: 4);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: span, value: value);
        output.Advance(count: 4);
    }

    private void WriteUInt64(IBufferWriter<byte> output, ulong value)
    {
        var span = output.GetSpan(sizeHint: 8);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: span, value: value);
        output.Advance(count: 8);
    }

    private void WriteString(IBufferWriter<byte> output, string value)
    {
        var encoded = EncodeUtf8(value: value);
        var length = encoded.Length;

        if (length > MaximumStringBytes)
        {
            throw new ArgumentException(message: "Catalog string exceeds the format limit.", paramName: nameof(value));
        }

        WriteUInt32(output: output, value: checked((uint)length));
        var span = output.GetSpan(sizeHint: length);

        encoded.AsSpan()
            .CopyTo(destination: span);

        output.Advance(count: length);
    }

    private void WriteBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        WriteUInt32(output: output, value: checked((uint)value.Length));
        value.CopyTo(destination: output.GetSpan(sizeHint: value.Length));
        output.Advance(count: value.Length);
    }

    private void WriteBigInteger(IBufferWriter<byte> output, BigInteger value) =>
        WriteBytes(output: output, value: value.ToByteArray(isUnsigned: false, isBigEndian: false));

    private string? EmptyToNull(string value) =>
        value.Length == 0 ? null : value;

    private void WriteColumn(IBufferWriter<byte> output, CatalogColumn column)
    {
        WriteUInt64(output: output, value: column.Id.Value);
        WriteString(output: output, value: column.Name);
        WriteType(output: output, type: column.Type);
        WriteByte(output: output, value: column.IsNullable ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.IsRowGuidCol ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.IsComputedPersisted ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.Identity is null ? (byte)0 : (byte)1);
        WriteString(output: output, value: column.DefaultExpression ?? string.Empty);
        WriteString(output: output, value: column.ComputedExpression ?? string.Empty);

        if (column.Identity is { } identity)
        {
            WriteBigInteger(output: output, value: identity.Seed);
            WriteBigInteger(output: output, value: identity.Increment);

            WriteByte(output: output, value: identity.IsNotForReplication switch
            {
                null => (byte)0,
                false => (byte)1,
                true => (byte)2
            });
        }

        WriteByte(output: output, value: column.IsSparse ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.IsColumnSet ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.IsFileStream ? (byte)1 : (byte)0);
        WriteByte(output: output, value: column.IsHidden ? (byte)1 : (byte)0);
        WriteByte(output: output, value: (byte)column.GeneratedAlways);
        WriteByte(output: output, value: column.Encryption is null ? (byte)0 : (byte)column.Encryption.EncryptionType);
        WriteUInt16(output: output, value: 0);
        WriteString(output: output, value: column.MaskingFunction ?? string.Empty);
        WriteString(output: output, value: column.Encryption?.KeyName ?? string.Empty);
        WriteString(output: output, value: column.Encryption?.Algorithm ?? string.Empty);
    }

    private void WriteIndexedColumn(IBufferWriter<byte> output, CatalogIndexedColumn column)
    {
        WriteUInt64(output: output, value: column.ColumnId.Value);
        WriteByte(output: output, value: (byte)column.Direction);
        WriteByte(output: output, value: (byte)column.NullSortOrder);
        WriteString(output: output, value: column.Collation ?? string.Empty);
    }

    private void WriteType(IBufferWriter<byte> output, SqlType type)
    {
        WriteByte(output: output, value: (byte)type.Name);
        WriteByte(output: output, value: type.Precision ?? byte.MaxValue);
        WriteByte(output: output, value: type.Scale ?? byte.MaxValue);
        WriteByte(output: output, value: type.IsMax ? (byte)1 : (byte)0);
        WriteInt32(output: output, value: type.Length ?? -1);
        WriteUInt16(output: output, value: type.VectorDimensions ?? 0);
        WriteByte(output: output, value: type.VectorBaseType is null ? byte.MaxValue : (byte)type.VectorBaseType.Value);
        WriteByte(output: output, value: type.XmlContentKind is null ? (byte)0 : (byte)type.XmlContentKind.Value);
        WriteString(output: output, value: type.Collation ?? string.Empty);
        WriteByte(output: output, value: type.XmlSchemaCollection is null ? (byte)0 : (byte)1);

        if (type.XmlSchemaCollection is { } xmlSchema)
        {
            WriteString(output: output, value: xmlSchema.SchemaName);
            WriteString(output: output, value: xmlSchema.Name);
        }

        WriteByte(output: output, value: (byte)type.UserTypeKind);

        switch (type.UserTypeKind)
        {
            case SqlUserTypeKind.None:
                break;
            case SqlUserTypeKind.Alias:
                WriteString(output: output, value: type.UserTypeSchema!);
                WriteString(output: output, value: type.UserTypeName!);
                WriteByte(output: output, value: type.UserTypeNullable!.Value ? (byte)1 : (byte)0);
                WriteType(output: output, type: type.BaseType!);
                break;
            case SqlUserTypeKind.Clr:
                WriteString(output: output, value: type.UserTypeSchema!);
                WriteString(output: output, value: type.UserTypeName!);
                WriteString(output: output, value: type.ClrAssemblyName!);
                WriteString(output: output, value: type.ClrClassName!);
                WriteByte(output: output, value: (byte)type.ClrSerializationFormat!.Value);
                WriteInt32(output: output, value: type.ClrMaxByteSize ?? int.MinValue);
                WriteByte(output: output, value: type.ClrIsByteOrdered ? (byte)1 : (byte)0);
                WriteByte(output: output, value: type.ClrIsFixedLength ? (byte)1 : (byte)0);
                WriteString(output: output, value: type.ClrValidationMethodName ?? string.Empty);
                break;
            default:
                throw new ArgumentException(message: "Unknown SQL user-defined type kind.", paramName: nameof(type));
        }
    }

    private void ReadCatalogSetVersion(ref ReadableCatalogRecordContext reader, ushort version) =>
        reader.Version = version;

    private void ReadCatalogSetXmlSchemas(ref ReadableCatalogRecordContext reader, IEnumerable<SqlXmlSchemaCollection> schemas) =>
        reader.XmlSchemas = schemas.ToDictionary(keySelector: schema => (schema.SchemaName, schema.Name));

    private byte ReadCatalogByte(ref ReadableCatalogRecordContext reader)
    {
        ReadCatalogRequire(reader: ref reader, length: 1);
        return reader.Source[reader.Offset++];
    }

    private bool ReadCatalogBoolean(ref ReadableCatalogRecordContext reader) =>
        ReadCatalogByte(reader: ref reader) switch
    {
        0 => false,
        1 => true,
        _ => throw new StorageFormatException(message: "Invalid catalog Boolean value.")};

    private ushort ReadCatalogUInt16(ref ReadableCatalogRecordContext reader)
    {
        ReadCatalogRequire(reader: ref reader, length: 2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(source: reader.Source[reader.Offset..]);
        reader.Offset += 2;
        return value;
    }

    private uint ReadCatalogUInt32(ref ReadableCatalogRecordContext reader)
    {
        ReadCatalogRequire(reader: ref reader, length: 4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(source: reader.Source[reader.Offset..]);
        reader.Offset += 4;
        return value;
    }

    private int ReadCatalogInt32(ref ReadableCatalogRecordContext reader)
    {
        ReadCatalogRequire(reader: ref reader, length: 4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(source: reader.Source[reader.Offset..]);
        reader.Offset += 4;
        return value;
    }

    private ulong ReadCatalogUInt64(ref ReadableCatalogRecordContext reader)
    {
        ReadCatalogRequire(reader: ref reader, length: 8);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(source: reader.Source[reader.Offset..]);
        reader.Offset += 8;
        return value;
    }

    private int ReadCatalogCount(ref ReadableCatalogRecordContext reader, bool nonZero = false)
    {
        var value = ReadCatalogUInt32(reader: ref reader);

        if (value > MaximumRecordCount || nonZero && value == 0)
        {
            throw new StorageFormatException(message: "Invalid catalog record count.");
        }

        return checked((int)value);
    }

    private string ReadCatalogString(ref ReadableCatalogRecordContext reader)
    {
        var rawLength = ReadCatalogUInt32(reader: ref reader);

        if (rawLength > MaximumStringBytes)
        {
            throw new StorageFormatException(message: "Catalog string exceeds the format limit.");
        }

        var length = checked((int)rawLength);
        ReadCatalogRequire(reader: ref reader, length: length);

        try
        {
            var value = DecodeUtf8(value: reader.Source.Slice(start: reader.Offset, length: length)
            .ToArray());

            reader.Offset += length;
            return value;
        }
        catch (DecoderFallbackException exception)
        {
            throw new StorageFormatException(message: "Catalog string is not valid UTF-8.", innerException: exception);
        }
    }

    private byte[] ReadCatalogBytes(ref ReadableCatalogRecordContext reader)
    {
        var rawLength = ReadCatalogUInt32(reader: ref reader);

        if (rawLength > int.MaxValue)
        {
            throw new StorageFormatException(message: "Catalog byte array is too large.");
        }

        var length = (int)rawLength;
        ReadCatalogRequire(reader: ref reader, length: length);

        var result = reader.Source.Slice(start: reader.Offset, length: length)
            .ToArray();

        reader.Offset += length;
        return result;
    }

    private BigInteger ReadCatalogBigInteger(ref ReadableCatalogRecordContext reader)
    {
        var bytes = ReadCatalogBytes(reader: ref reader);

        if (bytes.Length == 0)
        {
            throw new StorageFormatException(message: "Catalog integer is empty.");
        }

        var value = new System.Numerics.BigInteger(value: bytes, isUnsigned: false, isBigEndian: false);

        if (!bytes.AsSpan()
            .SequenceEqual(other: value.ToByteArray(isUnsigned: false, isBigEndian: false)))
        {
            throw new StorageFormatException(message: "Catalog integer is not canonically encoded.");
        }

        return value;
    }

    private CatalogColumn ReadCatalogColumn(ref ReadableCatalogRecordContext reader)
    {
        var id = new sql_storage_engine.Models.Identifiers.ColumnId
        {
            Value = ReadCatalogUInt64(reader: ref reader)
        };

        var name = ReadCatalogString(reader: ref reader);
        var type = ReadCatalogType(reader: ref reader);
        var nullable = ReadCatalogBoolean(reader: ref reader);
        var rowGuid = ReadCatalogBoolean(reader: ref reader);
        var computedPersisted = ReadCatalogBoolean(reader: ref reader);
        var hasIdentity = ReadCatalogBoolean(reader: ref reader);
        var defaultExpression = ReadCatalogString(reader: ref reader);
        var computedExpression = ReadCatalogString(reader: ref reader);
        CatalogIdentity? identity = null;

        if (hasIdentity)
        {
            var seed = ReadCatalogBigInteger(reader: ref reader);
            var increment = ReadCatalogBigInteger(reader: ref reader);

            bool? replication = reader.Version < 13 ? null : ReadCatalogByte(reader: ref reader) switch
            {
                0 => null,
                1 => false,
                2 => true,
                _ => throw new StorageFormatException(message: "Invalid identity replication flag.")};

            identity = CreateCatalogIdentity(seed: seed, increment: increment, isNotForReplication: replication);
        }

        var sparse = ReadCatalogBoolean(reader: ref reader);
        var columnSet = ReadCatalogBoolean(reader: ref reader);
        var fileStream = ReadCatalogBoolean(reader: ref reader);
        var hidden = ReadCatalogBoolean(reader: ref reader);
        var generated = (CatalogGeneratedAlwaysKind)ReadCatalogByte(reader: ref reader);
        var rawEncryption = ReadCatalogByte(reader: ref reader);

        if (ReadCatalogUInt16(reader: ref reader) != 0)
        {
            throw new StorageFormatException(message: "Reserved column-facet bytes must be zero.");
        }

        var masking = EmptyToNull(value: ReadCatalogString(reader: ref reader));
        var encryptionKey = EmptyToNull(value: ReadCatalogString(reader: ref reader));
        var encryptionAlgorithm = EmptyToNull(value: ReadCatalogString(reader: ref reader));
        CatalogColumnEncryption? encryption = rawEncryption == 0 ? null : CreateCatalogColumnEncryption(keyName: encryptionKey ?? throw new StorageFormatException(message: "Encrypted column has no key."), encryptionType: (CatalogEncryptionType)rawEncryption, algorithm: encryptionAlgorithm ?? throw new StorageFormatException(message: "Encrypted column has no algorithm."));

        try
        {
            return CreateCatalogColumn(columnId: id, name: name, sqlType: type, isNullable: nullable, defaultExpression: EmptyToNull(value: defaultExpression), catalogIdentity: identity, isRowGuidCol: rowGuid, computedExpression: EmptyToNull(value: computedExpression), isComputedPersisted: computedPersisted, isSparse: sparse, isColumnSet: columnSet, isFileStream: fileStream, maskingFunction: masking, catalogColumnEncryption: encryption, generatedAlways: generated, isHidden: hidden);
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Invalid catalog column record.", innerException: exception);
        }
    }

    private CatalogIndexedColumn ReadCatalogIndexedColumn(ref ReadableCatalogRecordContext reader)
    {
        var columnId = new sql_storage_engine.Models.Identifiers.ColumnId
        {
            Value = ReadCatalogUInt64(reader: ref reader)
        };

        var direction = (SortDirection)ReadCatalogByte(reader: ref reader);
        var nullOrder = (NullSortOrder)ReadCatalogByte(reader: ref reader);
        var collation = ReadCatalogString(reader: ref reader);

        try
        {
            return CreateCatalogIndexedColumn(columnId: columnId, direction: direction, nullSortOrder: nullOrder, collation: collation.Length == 0 ? null : collation);
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Invalid indexed-column record.", innerException: exception);
        }
    }

    private SqlType ReadCatalogType(ref ReadableCatalogRecordContext reader)
    {
        var name = (SqlTypeName)ReadCatalogByte(reader: ref reader);

        if (!Enum.IsDefined(value: name))
        {
            throw new StorageFormatException(message: "Unknown SQL type name.");
        }

        var rawPrecision = ReadCatalogByte(reader: ref reader);
        var rawScale = ReadCatalogByte(reader: ref reader);
        var isMax = ReadCatalogBoolean(reader: ref reader);
        var rawLength = ReadCatalogInt32(reader: ref reader);

        if (rawLength < -1)
        {
            throw new StorageFormatException(message: "Invalid SQL type length.");
        }

        var rawDimensions = ReadCatalogUInt16(reader: ref reader);
        var rawVectorType = ReadCatalogByte(reader: ref reader);
        var rawXmlContentKind = ReadCatalogByte(reader: ref reader);
        var collation = ReadCatalogString(reader: ref reader);
        SqlXmlSchemaCollection? xmlSchema = null;

        if (ReadCatalogBoolean(reader: ref reader))
        {
            var schemaName = ReadCatalogString(reader: ref reader);
            var collectionName = ReadCatalogString(reader: ref reader);

            if (reader.XmlSchemas is null || !reader.XmlSchemas.TryGetValue(key: (schemaName, collectionName), value: out xmlSchema))
            {
                throw new StorageFormatException(message: $"Typed XML references unknown collection [{schemaName}].[{collectionName}].");
            }
        }

        var userTypeKind = (SqlUserTypeKind)ReadCatalogByte(reader: ref reader);

        if (!Enum.IsDefined(value: userTypeKind))
        {
            throw new StorageFormatException(message: "Unknown SQL user-defined type kind.");
        }

        string? userSchema = null, userName = null, assemblyName = null, className = null;
        SqlType? baseType = null;
        bool? userNullable = null;
        SqlClrSerializationFormat? serializationFormat = null;
        int? maxByteSize = null;
        var byteOrdered = false;
        var fixedLength = false;
        string? validationMethodName = null;

        if (userTypeKind == SqlUserTypeKind.Alias)
        {
            userSchema = ReadCatalogString(reader: ref reader);
            userName = ReadCatalogString(reader: ref reader);
            userNullable = ReadCatalogBoolean(reader: ref reader);
            baseType = ReadCatalogType(reader: ref reader);
        }
        else
        {
            if (userTypeKind == SqlUserTypeKind.Clr)
            {
                userSchema = ReadCatalogString(reader: ref reader);
                userName = ReadCatalogString(reader: ref reader);
                assemblyName = ReadCatalogString(reader: ref reader);
                className = ReadCatalogString(reader: ref reader);
                serializationFormat = (SqlClrSerializationFormat)ReadCatalogByte(reader: ref reader);

                if (!Enum.IsDefined(value: serializationFormat.Value))
                {
                    throw new StorageFormatException(message: "Unknown CLR serialization format.");
                }

                var rawMaxByteSize = ReadCatalogInt32(reader: ref reader);
                maxByteSize = rawMaxByteSize == int.MinValue ? null : rawMaxByteSize;
                byteOrdered = ReadCatalogBoolean(reader: ref reader);
                fixedLength = ReadCatalogBoolean(reader: ref reader);
                var rawValidationMethod = ReadCatalogString(reader: ref reader);
                validationMethodName = rawValidationMethod.Length == 0 ? null : rawValidationMethod;
            }
        }

        try
        {
            return CreateRestoreSqlType(name: name, precision: rawPrecision == byte.MaxValue ? null : rawPrecision, scale: rawScale == byte.MaxValue ? null : rawScale, length: rawLength < 0 ? null : rawLength, isMax: isMax, collation: collation.Length == 0 ? null : collation, vectorDimensions: rawDimensions == 0 ? null : rawDimensions, vectorBaseType: rawVectorType == byte.MaxValue ? null : (SqlVectorBaseType)rawVectorType, xmlContentKind: rawXmlContentKind == 0 ? null : (SqlXmlContentKind)rawXmlContentKind, sqlXmlSchemaCollection: xmlSchema, userTypeKind: userTypeKind, userTypeSchema: userSchema, userTypeName: userName, sqlType: baseType, userTypeNullable: userNullable, clrAssemblyName: assemblyName, clrClassName: className, clrSerializationFormat: serializationFormat, clrMaxByteSize: maxByteSize, clrIsByteOrdered: byteOrdered, clrIsFixedLength: fixedLength, clrValidationMethodName: validationMethodName);
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Invalid SQL type declaration.", innerException: exception);
        }
    }

    private void ReadCatalogRequire(ref ReadableCatalogRecordContext reader, int length)
    {
        if (length < 0 || reader.Offset > reader.Source.Length - length)
        {
            throw new StorageFormatException(message: "Bootstrap catalog record is truncated.");
        }
    }

    private SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(string schemaName, string name, IEnumerable<string> definitions) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 3, SqlXmlSchemaCollection = new SqlXmlSchemaCollection { SchemaName = schemaName, Name = name, DefinitionSource = definitions } })
            .PrepareSqlXmlSchemaCollectionResult!;

    private IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 4, SqlXmlSchemaCollection = sqlXmlSchemaCollection })
            .ReadSqlXmlSchemaCollectionDefinitionsResult!;

    private CatalogAssembly CreateCatalogAssembly(string name, ReadOnlySpan<byte> image = default, string? version = null, string? culture = null, string? publicKeyToken = null, CatalogAssemblyPermissionSet permissionSet = CatalogAssemblyPermissionSet.Safe) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 5, CatalogAssembly = new CatalogAssembly { Name = name, Version = version, Culture = culture, PublicKeyToken = publicKeyToken, PermissionSet = permissionSet }, Image = image.ToArray() })
            .PrepareCatalogAssemblyResult!;

    private ReadOnlyMemory<byte> ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 6, CatalogAssembly = catalogAssembly })
            .ReadCatalogAssemblyImageResult!;

    private IReadOnlyList<string> EncodeCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 7, CatalogFullTextIndexOptions = catalogFullTextIndexOptions })
            .EncodeCatalogFullTextIndexOptionsResult!;

    private CatalogFullTextIndexOptions DecodeCatalogFullTextIndexOptions(IReadOnlyList<string> values) =>
        catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 8, Values = values })
            .DecodeCatalogFullTextIndexOptionsResult!;

    private CatalogCheckConstraint CreateCatalogCheckConstraint(string name, string expression) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 9, CatalogCheckConstraint = new CatalogCheckConstraint { Name = name, Expression = expression } })
            .PrepareCatalogCheckConstraintResult!;

    private CatalogSystemVersioning CreateCatalogSystemVersioning(TableId tableId, ulong periodStartColumnId, ulong periodEndColumnId) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 10, CatalogSystemVersioning = new CatalogSystemVersioning { HistoryTableId = tableId, PeriodStartColumnId = new ColumnId { Value = periodStartColumnId }, PeriodEndColumnId = new ColumnId { Value = periodEndColumnId } } })
            .PrepareCatalogSystemVersioningResult!;

    private CatalogGraphTable CreateCatalogGraphTableNode(ulong identityColumnId, ulong identityIndexId) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 11, CatalogGraphTable = new CatalogGraphTable { Kind = GraphTableKind.Node, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId } } })
            .PrepareCatalogGraphTableResult!;

    private CatalogGraphTable CreateCatalogGraphTableEdge(ulong identityColumnId, ulong identityIndexId, ulong fromNodeTableId, ulong fromNodeColumnId, ulong outgoingIndexId, ulong toNodeTableId, ulong toNodeColumnId, ulong incomingIndexId) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 11, CatalogGraphTable = new CatalogGraphTable { Kind = GraphTableKind.Edge, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId }, FromNodeTableId = new TableId { Value = fromNodeTableId }, FromNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = fromNodeColumnId }, OutgoingIndexId = new IndexId { Value = outgoingIndexId }, ToNodeTableId = new TableId { Value = toNodeTableId }, ToNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = toNodeColumnId }, IncomingIndexId = new IndexId { Value = incomingIndexId } } })
            .PrepareCatalogGraphTableResult!;

    private CatalogTable CreateCatalogTable(TableId tableId, string name, ulong schemaVersion, PageId pageId, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, BigInteger? nextIdentityValue = null, string databaseName = "default", string schemaName = "dbo", CatalogSystemVersioning? catalogSystemVersioning = null, CatalogGraphTable? catalogGraphTable = null) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 12, CatalogTable = new CatalogTable { IdSource = tableId, NameSource = name, SchemaVersionSource = schemaVersion, FirstHeapPageIdSource = pageId, ColumnsSource = columns, CheckConstraintsSource = checkConstraints, NextIdentityValueSource = nextIdentityValue, DatabaseNameSource = databaseName, SchemaNameSource = schemaName, SystemVersioningSource = catalogSystemVersioning, GraphSource = catalogGraphTable } })
            .PrepareCatalogTableResult!;

    private CatalogIndexedColumn CreateCatalogIndexedColumn(ColumnId columnId, SortDirection direction, NullSortOrder nullSortOrder, string? collation = null) =>
        catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 13, CatalogIndexedColumn = new CatalogIndexedColumn { ColumnId = columnId, Direction = direction, NullSortOrder = nullSortOrder, Collation = collation } })
            .PrepareCatalogIndexedColumnResult!;

    private CatalogSpecializedIndexOptions CreateJsonCatalogSpecializedIndexOptions(params string[] paths) =>
        PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Json, PathSource = paths, RequirePaths = true });

    private CatalogSpecializedIndexOptions CreateSpatialCatalogSpecializedIndexOptions(int srid) =>
        PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Spatial, SpatialSrid = srid });

    private CatalogSpecializedIndexOptions CreateVectorCatalogSpecializedIndexOptions(SqlVectorDistanceMetric metric = SqlVectorDistanceMetric.Cosine) =>
        PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Vector, VectorMetric = metric });

    private CatalogSpecializedIndexOptions CreateFullTextCatalogSpecializedIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.FullText, FullTextOptions = catalogFullTextIndexOptions });

    private CatalogSpecializedIndexOptions CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind kind, IReadOnlyDictionary<string, string> namespaces, params string[] paths) =>
        PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Xml, XmlIndexKind = kind, PathSource = paths, RequirePaths = true, NamespaceSource = namespaces, RequireNamespaces = true });

    private CatalogIndex CreateCatalogIndex(IndexId indexId, string name, TableId tableId, PageId pageId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? catalogSpecializedIndexOptions = null, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, bool isPrimaryKey = false, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false) =>
        PrepareCatalogIndex(catalogIndex: new CatalogIndex { Id = indexId, Name = name, TableId = tableId, RootPageId = pageId, IsUnique = isUnique, ColumnSource = columns, SpecializedOptions = catalogSpecializedIndexOptions, StorageKind = storageKind, IsPrimaryKey = isPrimaryKey, IncludedColumnSource = includedColumns, IgnoreDuplicateKey = ignoreDuplicateKey });

    private SqlType CreateRestoreSqlType(SqlTypeName name, byte? precision, byte? scale, int? length, bool isMax, string? collation, ushort? vectorDimensions, SqlVectorBaseType? vectorBaseType, SqlXmlContentKind? xmlContentKind = null, SqlXmlSchemaCollection? sqlXmlSchemaCollection = null, SqlUserTypeKind userTypeKind = SqlUserTypeKind.None, string? userTypeSchema = null, string? userTypeName = null, SqlType? sqlType = null, bool? userTypeNullable = null, string? clrAssemblyName = null, string? clrClassName = null, SqlClrSerializationFormat? clrSerializationFormat = null, int? clrMaxByteSize = null, bool clrIsByteOrdered = false, bool clrIsFixedLength = false, string? clrValidationMethodName = null) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 14, SqlTypeContext = new SqlTypeContext { InputSqlTypeNameName = name, InputbytePrecision = precision, InputbyteScale = scale, InputintLength = length, InputboolIsMax = isMax, InputstringCollation = collation, InputushortVectorDimensions = vectorDimensions, InputSqlVectorBaseTypeVectorBaseType = vectorBaseType, InputSqlXmlContentKindXmlContentKind = xmlContentKind, InputSqlXmlSchemaCollectionXmlSchemaCollection = sqlXmlSchemaCollection, InputSqlUserTypeKindUserTypeKind = userTypeKind, InputstringUserTypeSchema = userTypeSchema, InputstringUserTypeName = userTypeName, InputSqlTypeBaseType = sqlType, InputboolUserTypeNullable = userTypeNullable, InputstringClrAssemblyName = clrAssemblyName, InputstringClrClassName = clrClassName, InputSqlClrSerializationFormatClrSerializationFormat = clrSerializationFormat, InputintClrMaxByteSize = clrMaxByteSize, InputboolClrIsByteOrdered = clrIsByteOrdered, InputboolClrIsFixedLength = clrIsFixedLength, InputstringClrValidationMethodName = clrValidationMethodName } })
            .ProcessSqlTypeContextRestore73Result!.SqlTypeResult!;

    private sql_storage_engine.Models.Catalog.CatalogScalarType CreateCatalogScalarType(SqlType sqlType) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 15, SqlTypeContext = new SqlTypeContext { SqlTypeSource = sqlType } })
            .CreateSqlTypeContextCatalogScalarTypeResult!.CatalogScalarTypeResult!;

    private CatalogTableTypeIndex CreateCatalogTableTypeIndex(string name, bool isPrimaryKey, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false, long? bucketCount = null) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 16, CatalogTableTypeIndex = new CatalogTableTypeIndex { NameSource = name, IsPrimaryKeySource = isPrimaryKey, IsUniqueSource = isUnique, ColumnsSource = columns, StorageKindSource = storageKind, IncludedColumnsSource = includedColumns, IgnoreDuplicateKeySource = ignoreDuplicateKey, BucketCountSource = bucketCount } })
            .PrepareCatalogTableTypeIndexResult!;

    private CatalogTableType CreateCatalogTableType(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, bool isMemoryOptimized = false) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 17, CatalogTableType = new CatalogTableType { SchemaNameSource = schemaName, NameSource = name, ColumnsSource = columns, IndexesSource = indexes, CheckConstraintsSource = checkConstraints, IsMemoryOptimizedSource = isMemoryOptimized } })
            .PrepareCatalogTableTypeResult!;

    private CatalogDefinition CreateCatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes, IEnumerable<CatalogScalarType>? scalarTypes = null, IEnumerable<CatalogTableType>? tableTypes = null, IEnumerable<SqlXmlSchemaCollection>? xmlSchemaCollections = null, IEnumerable<CatalogAssembly>? assemblies = null) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 18, CatalogDefinition = new CatalogDefinition { TablesSource = tables, IndexesSource = indexes, ScalarTypesSource = scalarTypes, TableTypesSource = tableTypes, XmlSchemaCollectionsSource = xmlSchemaCollections, AssembliesSource = assemblies } })
            .PrepareCatalogDefinitionResult!;

    private CatalogIdentity CreateCatalogIdentity(BigInteger seed, BigInteger increment, bool? isNotForReplication) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 19, CatalogIdentity = new CatalogIdentity { Seed = seed, Increment = increment, IsNotForReplication = isNotForReplication } })
            .PrepareCatalogIdentityResult!;

    private CatalogColumnEncryption CreateCatalogColumnEncryption(string keyName, CatalogEncryptionType encryptionType, string algorithm = "AEAD_AES_256_CBC_HMAC_SHA_256") =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 20, CatalogColumnEncryption = new CatalogColumnEncryption { KeyName = keyName, EncryptionType = encryptionType, Algorithm = algorithm } })
            .PrepareCatalogColumnEncryptionResult!;

    private CatalogColumn CreateCatalogColumn(ColumnId columnId, string name, SqlType sqlType, bool isNullable, string? defaultExpression = null, CatalogIdentity? catalogIdentity = null, bool isRowGuidCol = false, string? computedExpression = null, bool isComputedPersisted = false, bool isSparse = false, bool isColumnSet = false, bool isFileStream = false, string? maskingFunction = null, CatalogColumnEncryption? catalogColumnEncryption = null, CatalogGeneratedAlwaysKind generatedAlways = CatalogGeneratedAlwaysKind.None, bool isHidden = false) =>
        catalogTableSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 21, CatalogColumn = new CatalogColumn { IdSource = columnId, NameSource = name, TypeSource = sqlType, IsNullableSource = isNullable, DefaultExpressionSource = defaultExpression, IdentitySource = catalogIdentity, IsRowGuidColSource = isRowGuidCol, ComputedExpressionSource = computedExpression, IsComputedPersistedSource = isComputedPersisted, IsSparseSource = isSparse, IsColumnSetSource = isColumnSet, IsFileStreamSource = isFileStream, MaskingFunctionSource = maskingFunction, EncryptionSource = catalogColumnEncryption, GeneratedAlwaysSource = generatedAlways, IsHiddenSource = isHidden } })
            .PrepareCatalogColumnResult!;

    private CatalogSpecializedIndexOptions PrepareCatalogSpecializedIndexOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions) =>
        catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 22, CatalogSpecializedIndexOptions = catalogSpecializedIndexOptions })
            .PrepareCatalogSpecializedIndexOptionsResult!;

    private CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex) =>
        catalogIndexSchemas.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 24, CatalogIndex = catalogIndex })
            .PrepareCatalogIndexResult!;

    private byte[] EncodeUtf8(string value) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 25, CatalogTextEncodingContext = new CatalogTextEncodingContext { Text = value } })
            .EncodeCatalogTextEncodingContextResult!.Bytes!;

    private string DecodeUtf8(byte[] value) =>
        catalogDefinitionTransport.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 26, CatalogTextEncodingContext = new CatalogTextEncodingContext { Bytes = value } })
            .DecodeCatalogTextEncodingContextResult!.Text!;

    internal static CatalogCodecManagementService CreateCatalogCodecManagementService() =>
        new CatalogCodecManagementService(catalogTableSchemas: sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogTableSchemasCoordinationService.CreateCatalogTableSchemasCoordinationService(), catalogDefinitionTransport: sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogDefinitionTransportCoordinationService.CreateCatalogDefinitionTransportCoordinationService(), catalogIndexSchemas: sql_storage_engine.Services.Coordinations.CatalogCodecs.CatalogIndexSchemasCoordinationService.CreateCatalogIndexSchemasCoordinationService());
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Managements.CatalogIndexKeys;
/// <summary>Builds deterministic composite keys from catalog ordering metadata.</summary>
internal sealed partial class CatalogIndexKeyRequestManagementService : ICatalogIndexKeyRequestManagementService
{
    private readonly sql_storage_engine.Services.Coordinations.CatalogIndexProjections.CatalogIndexProjectionContextCoordinationService projections;
    private readonly sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService types;
    private readonly sql_storage_engine.Services.Coordinations.RowFormats.RowFormatContextCoordinationService formats;
    public CatalogIndexKeyRequestManagementService(sql_storage_engine.Services.Coordinations.CatalogIndexProjections.CatalogIndexProjectionContextCoordinationService projections, sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService types, sql_storage_engine.Services.Coordinations.RowFormats.RowFormatContextCoordinationService formats)
    {
        this.projections = projections;
        this.types = types;
        this.formats = formats;
    }

    public CatalogIndexKeyRequest PrepareCatalogIndexKeyRequest(CatalogIndexKeyRequest catalogIndexKeyRequest) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexKeyRequest(catalogIndexKeyRequest: catalogIndexKeyRequest);

        switch (catalogIndexKeyRequest.Operation)
        {
            case 15:
                catalogIndexKeyRequest.Type = types.ProcessSqlTypeContextApplyDefaultCollation75(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexKeyRequest.Type, InputstringDefaultCollation = catalogIndexKeyRequest.DefaultCollation })
            .SqlTypeResult;
                break;
            case 1:
                catalogIndexKeyRequest.Entries = EncodeEntries(row: catalogIndexKeyRequest.Row!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
            case 2:
                catalogIndexKeyRequest.Key = EncodeJsonPathValue(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!, value: catalogIndexKeyRequest.SqlValue!);
                break;
            case 3:
                catalogIndexKeyRequest.Key = EncodeJsonPathExists(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!);
                break;
            case 4:
                catalogIndexKeyRequest.Key = EncodeXmlPathValue(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!, value: catalogIndexKeyRequest.Value!);
                break;
            case 5:
                catalogIndexKeyRequest.Key = EncodeXmlPathExists(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!);
                break;
            case 6:
                catalogIndexKeyRequest.Key = EncodeFullTextTerm(index: catalogIndexKeyRequest.Index!, table: catalogIndexKeyRequest.Table!, fullTextSearchRequest: catalogIndexKeyRequest.Request!);
                break;
            case 7:
                catalogIndexKeyRequest.Key = Encode(row: catalogIndexKeyRequest.Row!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
            case 8:
                catalogIndexKeyRequest.PayloadResult = EncodeIncludedValues(row: catalogIndexKeyRequest.Row!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
            case 9:
                catalogIndexKeyRequest.IncludedValues = DecodeIncludedValues(payload: catalogIndexKeyRequest.Payload!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
            case 10:
                catalogIndexKeyRequest.Key = EncodeValues(values: catalogIndexKeyRequest.Values!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
            case 12:
                catalogIndexKeyRequest.Entries = EncodeJsonPathValueKeys(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!, value: catalogIndexKeyRequest.SqlValue!);
                break;
            case 13:
                catalogIndexKeyRequest.Entries = EncodeJsonPathExistsKeys(index: catalogIndexKeyRequest.Index!, path: catalogIndexKeyRequest.Path!);
                break;
            case 14:
                catalogIndexKeyRequest.Key = EncodeValues(values: catalogIndexKeyRequest.Values!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!, maximumKeyBytes: catalogIndexKeyRequest.MaximumKeyBytes);
                break;
            case 11:
                catalogIndexKeyRequest.Key = EncodePrefix(values: catalogIndexKeyRequest.Values!, table: catalogIndexKeyRequest.Table!, index: catalogIndexKeyRequest.Index!);
                break;
        }

        return catalogIndexKeyRequest;
    });
    private const int MaximumClusteredKeyBytes = 900;
    private const int MaximumNonClusteredKeyBytes = 1_700;
    private const int MaximumSqlVariantKeyBytes = 900;
    private IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> EncodeEntries(Row row, CatalogTable table, CatalogIndex index)
    {
        if (index.Method == CatalogIndexMethod.FullText)
        {
            var fullTextValue = SourceValue(row: row, table: table, index: index);

            if (fullTextValue.IsNull)
            {
                return[];
            }

            var options = index.SpecializedOptions!.FullTextOptions!;

            return TokenizeCatalogFullTextIndexOptionsDocument(document: ((TextSqlValue)fullTextValue).Value, catalogFullTextIndexOptions: options)
            .Select(selector: token => EncodeFullTextToken(index: index, table: table, token: token))
            .Distinct()
            .OrderBy(keySelector: key => key.Content, comparer: System.StringComparer.Ordinal)
            .ToArray();
        }

        if (index.Method == CatalogIndexMethod.Spatial && index.SpecializedOptions!.SpatialSrid is { } requiredSrid)
        {
            var spatialValue = SourceValue(row: row, table: table, index: index);

            if (spatialValue.IsNull)
            {
                return[];
            }

            var actualSrid = ((SpatialSqlValue)spatialValue).Value.Srid;

            if (actualSrid != requiredSrid)
            {
                throw new ArgumentException(message: $"Spatial index '{index.Name}' requires SRID {requiredSrid}; the row contains SRID {actualSrid}.", paramName: nameof(row));
            }
        }

        if (index.Method == CatalogIndexMethod.Vector && index.SpecializedOptions!.VectorMetric == SqlVectorDistanceMetric.Cosine)
        {
            var vectorValue = SourceValue(row: row, table: table, index: index);

            if (!vectorValue.IsNull && ReadVector(vectorSqlValue: ((VectorSqlValue)vectorValue))
            .All(predicate: component => component == 0f))
            {
                throw new ArgumentException(message: $"Cosine vector index '{index.Name}' cannot contain a zero vector.", paramName: nameof(row));
            }
        }

        sql_storage_engine.Models.Indexes.IndexKey documentKey = Encode(row: row, table: table, index: index);

        if (index.Method is not (CatalogIndexMethod.Json or CatalogIndexMethod.Xml))
        {
            return[documentKey];
        }

        var value = SourceValue(row: row, table: table, index: index);

        if (value.IsNull)
        {
            return[documentKey];
        }

        var entries = new HashSet<sql_storage_engine.Models.Indexes.IndexKey>
        {
            documentKey
        };

        if (index.Method == CatalogIndexMethod.Json)
        {
            for (var ordinal = 0; ordinal < index.SpecializedOptions!.JsonPaths.Count; ordinal++)
            {
                var promotedPath = index.SpecializedOptions.JsonPaths[ordinal];
                var selected = ReadJsonValues(json: ((TextSqlValue)value).Value, path: promotedPath);

                if (selected.Length != 0)
                {
                    entries.Add(item: EncodePathExists(prefix: 3, ordinal: ordinal));
                }

                foreach (var item in selected)
                {
                    entries.Add(item: EncodePathValue(prefix: 2, ordinal: ordinal, value: EncodeJsonObservation(kind: item.Kind, raw: item.Raw, text: item.Text)));
                }

                var normalizedPath = NormalizeJsonPath(path: promotedPath);

                var descendants = ProcessDocument(catalogIndexDocumentContext: new CatalogIndexDocumentContext { Operation = 2, Json = ((TextSqlValue)value).Value, Path = promotedPath, NormalizedPath = normalizedPath })
            .Descendants!;

                foreach (var item in descendants)
                {
                    entries.Add(item: EncodeNamedPathExists(prefix: 5, path: item.Path));
                    entries.Add(item: EncodeNamedPathValue(prefix: 4, path: item.Path, value: EncodeJsonObservation(kind: item.Kind, raw: item.Raw, text: item.Text)));
                }
            }
        }
        else
        {
            var nodes = SelectXmlNodes(xml: ((TextSqlValue)value).Value, index: index);

            foreach (var node in nodes)
            {
                entries.Add(item: EncodeNamedPathExists(prefix: 3, path: node.Path));
                entries.Add(item: EncodeNamedPathValue(prefix: 2, path: node.Path, value: ReadUtf8Bytes(value: node.Value)));
            }
        }

        return entries.OrderBy(keySelector: key => key.Content, comparer: System.StringComparer.Ordinal)
            .ToArray();
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeJsonPathValue(CatalogIndex index, string path, SqlValue value)
    {
        var keys = EncodeJsonPathValueKeys(index: index, path: path, value: value);

        if (keys.Count != 1)
        {
            throw new ArgumentException(message: "The JSON path expands to multiple index seeks; use IStorageIndex.FindJsonPathAsync.", paramName: nameof(path));
        }

        return keys[0];
    }

    private IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> EncodeJsonPathValueKeys(CatalogIndex index, string path, SqlValue value)
    {
        if (index.Method != CatalogIndexMethod.Json)
        {
            throw new ArgumentException(message: "Index is not a JSON index.", paramName: nameof(index));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        ArgumentNullException.ThrowIfNull(argument: value);
        var(ordinal, normalized, exact) = ResolveJsonLookupPath(index: index, path: path);

        if (exact)
        {
            return[EncodePathValue(prefix: 2, ordinal: ordinal, value: EncodeJsonValue(value: value))];
        }

        var encodedValue = EncodeJsonValue(value: value);

        return ExpandJsonLookupPaths(path: normalized)
            .Select(selector: candidate => EncodeNamedPathValue(prefix: 4, path: candidate, value: encodedValue))
            .ToArray();
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeJsonPathExists(CatalogIndex index, string path)
    {
        var keys = EncodeJsonPathExistsKeys(index: index, path: path);

        if (keys.Count != 1)
        {
            throw new ArgumentException(message: "The JSON path expands to multiple index seeks; use IStorageIndex.FindJsonPathExistsAsync.", paramName: nameof(path));
        }

        return keys[0];
    }

    private IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> EncodeJsonPathExistsKeys(CatalogIndex index, string path)
    {
        if (index.Method != CatalogIndexMethod.Json)
        {
            throw new ArgumentException(message: "Index is not a JSON index.", paramName: nameof(index));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        var(ordinal, normalized, exact) = ResolveJsonLookupPath(index: index, path: path);

        if (exact)
        {
            return[EncodePathExists(prefix: 3, ordinal: ordinal)];
        }

        return ExpandJsonLookupPaths(path: normalized)
            .Select(selector: candidate => EncodeNamedPathExists(prefix: 5, path: candidate))
            .ToArray();
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeXmlPathValue(CatalogIndex index, string path, string value)
    {
        if (index.Method != CatalogIndexMethod.Xml)
        {
            throw new ArgumentException(message: "Index is not an XML index.", paramName: nameof(index));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        ArgumentNullException.ThrowIfNull(argument: value);
        ValidateXmlLookupPath(index: index, path: path);
        return EncodeNamedPathValue(prefix: 2, path: NormalizeXmlLookupPath(index: index, path: path), value: ReadUtf8Bytes(value: value));
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeXmlPathExists(CatalogIndex index, string path)
    {
        if (index.Method != CatalogIndexMethod.Xml)
        {
            throw new ArgumentException(message: "Index is not an XML index.", paramName: nameof(index));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        ValidateXmlLookupPath(index: index, path: path);
        return EncodeNamedPathExists(prefix: 3, path: NormalizeXmlLookupPath(index: index, path: path));
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeFullTextTerm(CatalogIndex index, CatalogTable table, FullTextSearchRequest fullTextSearchRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: index);
        ArgumentNullException.ThrowIfNull(argument: table);
        ArgumentNullException.ThrowIfNull(argument: fullTextSearchRequest);

        if (index.Method != CatalogIndexMethod.FullText)
        {
            throw new ArgumentException(message: "Index is not a full-text index.", paramName: nameof(index));
        }

        var options = index.SpecializedOptions!.FullTextOptions!;

        if (!StringComparer.Ordinal.Equals(x: fullTextSearchRequest.Language, y: options.Language))
        {
            throw new ArgumentException(message: $"Full-text index '{index.Name}' requires language identity '{options.Language}'.", paramName: nameof(fullTextSearchRequest));
        }

        return EncodeFullTextToken(index: index, table: table, token: ParseCatalogFullTextIndexOptionsExactTerm(term: fullTextSearchRequest.Term, catalogFullTextIndexOptions: options));
    }

    private sql_storage_engine.Models.Indexes.IndexKey Encode(Row row, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(argument: row);
        ArgumentNullException.ThrowIfNull(argument: table);
        ArgumentNullException.ThrowIfNull(argument: index);

        if (index.TableId != table.Id)
        {
            throw new ArgumentException(message: "Index does not belong to the table.", paramName: nameof(index));
        }

        ValidateFor(table: table, index: index);

        if (row.Values.Count != table.Columns.Count)
        {
            throw new ArgumentException(message: "Row width does not match the table definition.", paramName: nameof(row));
        }

        return EncodeValues(values: index.Columns.Select(selector: indexed => row.Values[table.Columns.Select(selector: (column, offset) => (column, offset))
            .Single(predicate: item => item.column.Id == indexed.ColumnId)
            .offset])
            .ToArray(), table: table, index: index);
    }

    private SqlValue SourceValue(Row row, CatalogTable table, CatalogIndex index)
    {
        if (row.Values.Count != table.Columns.Count)
        {
            throw new ArgumentException(message: "Row width does not match the table definition.", paramName: nameof(row));
        }

        var columnId = index.Columns.Single()
            .ColumnId;

        var position = table.Columns.Select(selector: (column, offset) => (column, offset))
            .Single(predicate: item => item.column.Id == columnId)
            .offset;

        return row.Values[position];
    }

    private byte[] EncodeIncludedValues(Row row, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(argument: row);
        ArgumentNullException.ThrowIfNull(argument: table);
        ArgumentNullException.ThrowIfNull(argument: index);

        if (index.IncludedColumns.Count == 0)
        {
            return[];
        }

        if (row.Values.Count != table.Columns.Count)
        {
            throw new ArgumentException(message: "Row width does not match the table definition.", paramName: nameof(row));
        }

        var columns = IncludedColumns(table: table, index: index);

        var positions = columns.Select(selector: column => table.Columns.Select(selector: (candidate, position) => (candidate, position))
            .Single(predicate: item => item.candidate.Id == column.Id)
            .position)
            .ToArray();

        var schema = CreateIncludedColumnTableDefinition(columns: columns);
        var encoded = EncodeRow(row: CreateRow(values: positions.Select(selector: position => row.Values[position])), table: schema);

        if (encoded.Length > ushort.MaxValue)
        {
            throw new StorageResourceExhaustedException(message: "Covered index payload exceeds 65,535 bytes.");
        }

        return encoded;
    }

    private IReadOnlyDictionary<ColumnId, SqlValue> DecodeIncludedValues(ReadOnlySpan<byte> payload, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(argument: table);
        ArgumentNullException.ThrowIfNull(argument: index);

        if (index.IncludedColumns.Count == 0)
        {
            if (!payload.IsEmpty)
            {
                throw new StorageFormatException(message: "Index without INCLUDE columns has a covered payload.");
            }

            return new Dictionary<ColumnId, SqlValue>();
        }

        if (payload.IsEmpty)
        {
            throw new StorageCorruptionException(message: "Covered index entry has no included payload.");
        }

        var columns = IncludedColumns(table: table, index: index);
        var schema = CreateIncludedColumnTableDefinition(columns: columns);
        var row = DecodeRow(source: payload, table: schema);

        return columns.Select(selector: (column, position) => (column.Id, Value: row.Values[position]))
            .ToDictionary(keySelector: item => item.Id, elementSelector: item => item.Value);
    }

    private TableDefinition CreateIncludedColumnTableDefinition(IReadOnlyList<CatalogColumn> columns) =>
        CreateTableDefinition(columnDefinitions: columns.Select(selector: column => CreateColumnDefinition(columnId: column.Id, name: column.Name, sqlType: column.Type, isNullable: column.IsNullable, catalogColumnEncryption: column.Encryption, isSparse: column.IsSparse, isColumnSet: column.IsColumnSet)));

    private IReadOnlyList<CatalogColumn> IncludedColumns(CatalogTable table, CatalogIndex index)
    {
        ValidateFor(table: table, index: index);

        return index.IncludedColumns.Select(selector: id => table.Columns.Single(predicate: column => column.Id == id))
            .ToArray();
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index) =>
        EncodeValuesCore(values: values, table: table, index: index, maximumKeyBytesOverride: null, allowPrefix: false);

    private sql_storage_engine.Models.Indexes.IndexKey EncodePrefix(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index) =>
        EncodeValuesCore(values: values, table: table, index: index, maximumKeyBytesOverride: null, allowPrefix: true);

    private sql_storage_engine.Models.Indexes.IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index, int maximumKeyBytes) =>
        EncodeValuesCore(values: values, table: table, index: index, maximumKeyBytesOverride: maximumKeyBytes, allowPrefix: false);

    private sql_storage_engine.Models.Indexes.IndexKey EncodeValuesCore(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index, int? maximumKeyBytesOverride, bool allowPrefix)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
        ArgumentNullException.ThrowIfNull(argument: table);
        ArgumentNullException.ThrowIfNull(argument: index);
        ValidateFor(table: table, index: index);

        if ((!allowPrefix && values.Count != index.Columns.Count) || (allowPrefix && (values.Count == 0 || values.Count > index.Columns.Count)))
        {
            throw new ArgumentException(message: $"Expected {index.Columns.Count} index values, received {values.Count}.", paramName: nameof(values));
        }

        if (allowPrefix && index.Method != CatalogIndexMethod.BTree)
        {
            throw new ArgumentException(message: "Prefix bounds apply only to B-tree indexes.", paramName: nameof(index));
        }

        if (index.Method != CatalogIndexMethod.BTree)
        {
            return EncodeSpecialized(value: values.Single(), table: table, index: index);
        }

        var output = new ArrayBufferWriter<byte>();
        var logicalKeyBytes = 0;

        for (var indexOffset = 0; indexOffset < values.Count; indexOffset++)
        {
            var indexed = index.Columns[indexOffset];
            var column = table.Columns.Single(predicate: candidate => candidate.Id == indexed.ColumnId);

            if (!ReadSqlTypeCanBeBTreeKey(sqlType: column.Type))
            {
                throw new ArgumentException(message: $"SQL Server type {FormatSqlType(sqlType: column.Type)} cannot be a B-tree key.", paramName: nameof(index));
            }

            var value = values[indexOffset] ?? throw new ArgumentException(message: "Index values cannot contain null CLR references.", paramName: nameof(values));

            if (!value.IsNull)
            {
                CheckSqlTypeValue(sqlType: column.Type, sqlValue: value, columnName: column.Name);
            }

            value = NormalizeSqlTypeValue(sqlType: column.Type, sqlValue: value);

            if (column.Type.UserTypeKind == SqlUserTypeKind.Clr && value is BinarySqlValue clr && ReadBinarySqlValue(binarySqlValue: clr)
            .Length > MaximumNonClusteredKeyBytes)
            {
                throw new ArgumentException(message: "CLR UDT index value exceeds the nonclustered key limit.", paramName: nameof(values));
            }

            if (value is VariantSqlValue variantValue && GetVariantPayloadLength(variant: variantValue) > MaximumSqlVariantKeyBytes)
            {
                throw new ArgumentException(message: $"sql_variant index value exceeds {MaximumSqlVariantKeyBytes} bytes.", paramName: nameof(values));
            }

            logicalKeyBytes = checked(logicalKeyBytes + GetLogicalKeyBytes(type: column.Type, value: value));
            var segment = new ArrayBufferWriter<byte>();

            if (value.IsNull)
            {
                WriteByte(output: segment, value: indexed.NullSortOrder == NullSortOrder.First ? (byte)0 : (byte)255);
            }
            else
            {
                WriteByte(output: segment, value: indexed.NullSortOrder == NullSortOrder.First ? (byte)1 : (byte)254);

                if (column.Encryption is { } encryption)
                {
                    var logicalColumn = CreateColumnDefinition(columnId: column.Id, name: column.Name, sqlType: column.Type, isNullable: column.IsNullable);
                    WriteEscapedBytes(output: segment, bytes: Encrypt(metadata: encryption, plaintext: EncodeLogicalValue(value: value, column: logicalColumn)));
                }
                else
                {
                    switch (value)
                    {
                        case BooleanSqlValue boolean:
                            WriteByte(output: segment, value: boolean.Value ? (byte)1 : (byte)0);
                            break;
                        case IntegerSqlValue integer:
                            WriteSortableInt64(output: segment, value: integer.Value);
                            break;
                        case TextSqlValue text:
                            WriteEscapedBytes(output: segment, bytes: GetTextSortKey(value: text.Value, type: column.Type, indexCollation: indexed.Collation));
                            break;
                        case BinarySqlValue binary:
                            WriteEscapedBytes(output: segment, bytes: ReadBinarySqlValue(binarySqlValue: binary)
            .Span);
                            break;
                        case HierarchyIdSqlValue hierarchy:
                            WriteEscapedBytes(output: segment, bytes: ReadSqlHierarchyIdSortKey(sqlHierarchyId: hierarchy.Value));
                            break;
                        case DecimalSqlValue exact:
                            WriteDecimal(output: segment, value: exact.Value);
                            break;
                        case FloatSqlValue approximate:
                            WriteSortableDouble(output: segment, value: approximate.Value);
                            break;
                        case DateSqlValue date:
                            WriteSortableInt64(output: segment, value: date.Value.DayNumber);
                            break;
                        case TimeSqlValue time:
                            WriteSortableInt64(output: segment, value: time.Value.Ticks);
                            break;
                        case DateTimeSqlValue dateTime:
                            WriteSortableInt64(output: segment, value: dateTime.Value.Ticks);
                            break;
                        case DateTimeOffsetSqlValue dateTimeOffset:
                            WriteSortableInt64(output: segment, value: dateTimeOffset.Value.UtcTicks);
                            break;
                        case UniqueIdentifierSqlValue identifier:
                            WriteSqlGuid(output: segment, value: identifier.Value);
                            break;
                        case VariantSqlValue variant:
                            WriteByte(output: segment, value: GetVariantFamilyOrder(type: variant.DeclaredType.Name));
                            WriteVariant(output: segment, value: variant.Value, declaredType: variant.DeclaredType);
                            break;
                        default:
                            throw new ArgumentException(message: "Unsupported indexed SQL value.", paramName: nameof(values));
                    }
                }
            }

            if (indexed.Direction == SortDirection.Descending)
            {
                var encodedSegment = segment.WrittenSpan.ToArray();

                for (var offset = 1; offset < encodedSegment.Length; offset++)
                {
                    encodedSegment[offset] ^= 0xFF;
                }

                Write(output: output, value: encodedSegment);
            }
            else
            {
                Write(output: output, value: segment.WrittenSpan);
            }
        }

        var maximumKeyBytes = maximumKeyBytesOverride ?? (index.StorageKind == CatalogIndexStorageKind.Clustered ? MaximumClusteredKeyBytes : MaximumNonClusteredKeyBytes);

        if (logicalKeyBytes > maximumKeyBytes)
        {
            throw new ArgumentException(message: $"Actual index key is {logicalKeyBytes} bytes and exceeds the {maximumKeyBytes}-byte " + $"{index.StorageKind.ToString()
            .ToLowerInvariant()} key limit.", paramName: nameof(values));
        }

        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private int GetLogicalKeyBytes(SqlType type, SqlValue value)
    {
        if (value.IsNull)
        {
            return 0;
        }

        if (type.UserTypeKind == SqlUserTypeKind.Alias)
        {
            return GetLogicalKeyBytes(type: type.BaseType!, value: value);
        }

        if (type.UserTypeKind == SqlUserTypeKind.Clr)
        {
            return ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)value))
            .Length;
        }

        return type.Name switch
        {
            SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
            SqlTypeName.SmallInt => 2,
            SqlTypeName.Int or SqlTypeName.Real or SqlTypeName.SmallMoney or SqlTypeName.SmallDateTime => 4,
            SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.DateTime => 8,
            SqlTypeName.Float => type.Precision <= 24 ? 4 : 8,
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 : type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.Date => 3,
            SqlTypeName.Time => type.Scale <= 2 ? 3 : type.Scale <= 4 ? 4 : 5,
            SqlTypeName.DateTime2 => type.Scale <= 2 ? 6 : type.Scale <= 4 ? 7 : 8,
            SqlTypeName.DateTimeOffset => type.Scale <= 2 ? 8 : type.Scale <= 4 ? 9 : 10,
            SqlTypeName.Char or SqlTypeName.VarChar => CountSqlCollationBytes(sqlCollation: (ReadSqlTypeCollationMetadata(sqlType: type) ?? ReadDefaultSqlCollation()), value: ((TextSqlValue)value).Value),
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(((TextSqlValue)value).Value.Length * 2),
            SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.RowVersion or SqlTypeName.Timestamp => ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)value))
            .Length,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.HierarchyId => WriteSqlHierarchyId(sqlHierarchyId: ((HierarchyIdSqlValue)value).Value)
            .Length,
            SqlTypeName.SqlVariant => checked(GetVariantPayloadLength(variant: (VariantSqlValue)value) + 16),
            _ => throw new ArgumentException(message: $"SQL Server type {FormatSqlType(sqlType: type)} cannot be measured as a B-tree key.", paramName: nameof(type))};
    }

    private int GetVariantPayloadLength(VariantSqlValue variant) =>
        variant.Value switch
    {
        TextSqlValue text when variant.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar => checked(text.Value.Length * 2),
        TextSqlValue text => CountSqlCollationBytes(sqlCollation: (ReadSqlTypeCollationMetadata(sqlType: variant.DeclaredType) ?? ReadDefaultSqlCollation()), value: text.Value),
        BinarySqlValue binary => ReadBinarySqlValue(binarySqlValue: binary)
            .Length,
        _ => 32
    };

    private sql_storage_engine.Models.Indexes.IndexKey EncodeSpecialized(SqlValue value, CatalogTable table, CatalogIndex index)
    {
        var indexed = index.Columns.Single();
        var column = table.Columns.Single(predicate: candidate => candidate.Id == indexed.ColumnId);

        if (value.IsNull)
        {
            return CreateIndexKey(bytes: [0]);
        }

        CheckSqlTypeValue(sqlType: column.Type, sqlValue: value, columnName: column.Name);
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: 1);

        switch (index.Method)
        {
            case CatalogIndexMethod.Json:
                foreach (var path in index.SpecializedOptions!.JsonPaths)
                {
                    var selected = ReadJsonValues(json: ((TextSqlValue)value).Value, path: path);
                    WriteSortableInt64(output: output, value: selected.Length);

                    foreach (var item in selected)
                    {
                        WriteEscapedBytes(output: output, bytes: ReadUtf8Bytes(value: item.Raw));
                    }
                }

                break;
            case CatalogIndexMethod.Spatial:
                var spatial = ((SpatialSqlValue)value).Value;
                var bounds = ReadSqlSpatialBounds(sqlSpatial: spatial);
                WriteSortableDouble(output: output, value: bounds.MinX);
                WriteSortableDouble(output: output, value: bounds.MinY);
                WriteSortableDouble(output: output, value: bounds.MaxX);
                WriteSortableDouble(output: output, value: bounds.MaxY);
                WriteEscapedBytes(output: output, bytes: EncodeSqlSpatial(sqlSpatial: spatial));
                break;
            case CatalogIndexMethod.Vector:
                var vector = (VectorSqlValue)value;
                WriteByte(output: output, value: (byte)vector.BaseType);
                Span<byte> componentBytes = stackalloc byte[4];
                if (vector.BaseType == SqlVectorBaseType.Float32)
                {
                    foreach (var component in ReadVector(vectorSqlValue: vector))
                    {
                        BinaryPrimitives.WriteSingleBigEndian(destination: componentBytes, value: component);
                        Write(output: output, value: componentBytes);
                    }
                }
                else
                {
                    foreach (var component in ReadHalfVector(vectorSqlValue: vector))
                    {
                        BinaryPrimitives.WriteHalfBigEndian(destination: componentBytes, value: component);
                        Write(output: output, value: componentBytes[..2]);
                    }
                }

                break;
            case CatalogIndexMethod.Xml:
                var xml = ((TextSqlValue)value).Value;
                XDocument xmlDocument;
                var wrapped = false;
                try
                {
                    xmlDocument = XDocument.Parse(text: xml, options: LoadOptions.PreserveWhitespace);
                }
                catch (System.Xml.XmlException)
                {
                    xmlDocument = XDocument.Parse(text: "<sqlstore-root>" + xml + "</sqlstore-root>", options: LoadOptions.PreserveWhitespace);
                    wrapped = true;
                }

                foreach (var path in index.SpecializedOptions!.JsonPaths)
                {
                    var effectivePath = wrapped && path.StartsWith(value: '/') ? "/sqlstore-root" + (path == "/" ? "" : path) : path;
                    var selected = path == "/" ? [xmlDocument.Root!] : EvaluateXmlNodes(document: xmlDocument, effectivePath: effectivePath, declaredPath: path, namespaces: index.SpecializedOptions.XmlNamespaces);
                    WriteSortableInt64(output: output, value: selected.Count);

                    foreach (var node in selected)
                    {
                        var text = node is XElement element ? element.ToString(options: SaveOptions.DisableFormatting) : XmlNodeValue(node: node, path: path);
                        WriteEscapedBytes(output: output, bytes: ReadUtf8Bytes(value: text));
                    }
                }

                break;
            case CatalogIndexMethod.FullText:
                throw new InvalidOperationException(message: "Whole-value lookup is unavailable for a full-text index; use SearchFullTextAsync.");
            default:
                throw new ArgumentException(message: "Unknown specialized index method.", paramName: nameof(index));
        }

        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeFullTextToken(CatalogIndex index, CatalogTable table, string token)
    {
        var column = table.Columns.Single(predicate: candidate => candidate.Id == index.Columns.Single()
            .ColumnId);

        sql_storage_engine.Models.Rows.SqlCollation collation = ReadSqlTypeCollationMetadata(sqlType: column.Type) ?? throw new ArgumentException(message: "A full-text source column must have a collation.", paramName: nameof(table));
        var unicode = column.Type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText;
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: 6);
        WriteEscapedBytes(output: output, bytes: CalculateSqlCollationSortKey(sqlCollation: collation, value: token, unicode: unicode));
        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private IReadOnlyList<string> ExpandJsonLookupPaths(string path)
    {
        List<string> results = [""];
        var segmentStart = 0;
        var position = 0;

        while (position < path.Length)
        {
            if (path[position] == '"')
            {
                position++;

                while (position < path.Length && path[position] != '"')
                {
                    if (path[position++] == '\\')
                    {
                        position++;
                    }
                }

                position++;
                continue;
            }

            if (path[position] != '[')
            {
                position++;
                continue;
            }

            var end = path.IndexOf(value: ']', startIndex: position + 1);
            var prefix = path[segmentStart..position];
            var selector = path[(position + 1)..end];
            string[] selectors;

            if (selector.Contains(value: ','))
            {
                selectors = selector.Split(separator: ',', options: StringSplitOptions.TrimEntries);
            }
            else
            {
                if (selector.Contains(value: " to ", comparisonType: StringComparison.Ordinal))
                {
                    var bounds = selector.Split(separator: " to ", options: StringSplitOptions.TrimEntries);
                    var start = int.Parse(s: bounds[0], provider: System.Globalization.CultureInfo.InvariantCulture);
                    var finish = int.Parse(s: bounds[1], provider: System.Globalization.CultureInfo.InvariantCulture);

                    selectors = Enumerable.Range(start: start, count: checked(finish - start + 1))
            .Select(selector: index => index.ToString(provider: System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
                }
                else
                {
                    selectors = [selector];
                }
            }

            if ((long)results.Count * selectors.Length > ushort.MaxValue)
            {
                throw new ArgumentException(message: "JSON path expansion exceeds the maximum SQL JSON array cardinality.", paramName: nameof(path));
            }

            results = results.SelectMany(selector: result => selectors.Select(selector: selectorValue => result + prefix + "[" + selectorValue + "]"))
            .ToList();

            position = end + 1;
            segmentStart = position;
        }

        var suffix = path[segmentStart..];

        return results.Select(selector: result => result + suffix)
            .Distinct(comparer: StringComparer.Ordinal)
            .ToArray();
    }

    private (int Ordinal, string Normalized, bool Exact) ResolveJsonLookupPath(CatalogIndex index, string path)
    {
        _ = CreateJsonCatalogSpecializedIndexOptions(paths: path);
        var normalized = NormalizeJsonPath(path: path);

        for (var ordinal = 0; ordinal < index.SpecializedOptions!.JsonPaths.Count; ordinal++)
        {
            var promoted = NormalizeJsonPath(path: index.SpecializedOptions.JsonPaths[ordinal]);

            if (StringComparer.Ordinal.Equals(x: promoted, y: normalized))
            {
                return (ordinal, normalized, true);
            }

            if (normalized.StartsWith(value: promoted, comparisonType: StringComparison.Ordinal) && normalized.Length > promoted.Length && normalized[promoted.Length] is '.' or '[')
            {
                return (ordinal, normalized, false);
            }
        }

        throw new ArgumentException(message: "JSON path is not within a subtree promoted by this index.", paramName: nameof(path));
    }

    private byte[] EncodeJsonValue(SqlValue value) =>
        value switch
    {
        NullSqlValue => [4],
        TextSqlValue text => Tagged(tag: 1, payload: ReadUtf8Bytes(value: text.Value)),
        BooleanSqlValue boolean => [3, boolean.Value ? (byte)1 : (byte)0],
        IntegerSqlValue integer => Tagged(tag: 2, payload: ReadUtf8Bytes(value: NormalizeJsonNumber(value: integer.Value.ToString(provider: System.Globalization.CultureInfo.InvariantCulture)))),
        DecimalSqlValue exact => Tagged(tag: 2, payload: ReadUtf8Bytes(value: NormalizeJsonNumber(value: FormatSqlDecimal(sqlDecimal: exact.Value)))),
        FloatSqlValue approximate => Tagged(tag: 2, payload: ReadUtf8Bytes(value: NormalizeJsonNumber(value: approximate.Value.ToString(format: "R", provider: System.Globalization.CultureInfo.InvariantCulture)))),
        VariantSqlValue variant => EncodeJsonValue(value: variant.Value),
        _ => throw new ArgumentException(message: "JSON path lookup supports string, numeric, Boolean, and NULL scalar values.", paramName: nameof(value))};

    private byte[] Tagged(byte tag, ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[payload.Length + 1];
        bytes[0] = tag;
        payload.CopyTo(destination: bytes.AsSpan(start: 1));
        return bytes;
    }

    private string NormalizeJsonNumber(string value)
    {
        var position = 0;
        var negative = false;

        if (value.Length != 0 && value[0] == '-')
        {
            negative = true;
            position++;
        }

        var exponentAt = value.IndexOfAny(value0: 'e', value1: 'E');
        var mantissaEnd = exponentAt < 0 ? value.Length : exponentAt;
        var point = value.IndexOf(value: '.', startIndex: position, count: mantissaEnd - position);
        var fractionalDigits = point < 0 ? 0 : mantissaEnd - point - 1;
        var digits = (point < 0 ? value[position..mantissaEnd] : value[position..point] + value[(point + 1)..mantissaEnd]).TrimStart(trimChar: '0');

        if (digits.Length == 0)
        {
            return "0@0";
        }

        var exponent = exponentAt < 0 ? 0 : int.Parse(s: value[(exponentAt + 1)..], style: System.Globalization.NumberStyles.AllowLeadingSign, provider: System.Globalization.CultureInfo.InvariantCulture);
        var scale = checked(fractionalDigits - exponent);

        while (scale > 0 && digits.EndsWith(value: '0'))
        {
            digits = digits[..^1];
            scale--;
        }

        if (scale < 0)
        {
            digits += new string (c: '0', count: checked(-scale));
            scale = 0;
        }

        return (negative ? "-" : "+") + digits + "@" + scale.ToString(provider: System.Globalization.CultureInfo.InvariantCulture);
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodePathValue(byte prefix, int ordinal, ReadOnlySpan<byte> value)
    {
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: prefix);
        Span<byte> ordinalBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(destination: ordinalBytes, value: checked((ushort)ordinal));
        Write(output: output, value: ordinalBytes);
        WriteEscapedBytes(output: output, bytes: value);
        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodePathExists(byte prefix, int ordinal)
    {
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: prefix);
        Span<byte> ordinalBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(destination: ordinalBytes, value: checked((ushort)ordinal));
        Write(output: output, value: ordinalBytes);
        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeNamedPathValue(byte prefix, string path, ReadOnlySpan<byte> value)
    {
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: prefix);
        WriteEscapedBytes(output: output, bytes: ReadUtf8Bytes(value: path));
        WriteEscapedBytes(output: output, bytes: value);
        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private sql_storage_engine.Models.Indexes.IndexKey EncodeNamedPathExists(byte prefix, string path)
    {
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: prefix);
        WriteEscapedBytes(output: output, bytes: ReadUtf8Bytes(value: path));
        return CreateIndexKey(bytes: output.WrittenSpan);
    }

    private IReadOnlyList<(string Path, string Value)> SelectXmlNodes(string xml, CatalogIndex index)
    {
        XDocument document;
        var wrapped = false;

        try
        {
            document = XDocument.Parse(text: xml, options: LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            document = XDocument.Parse(text: "<sqlstore-root>" + xml + "</sqlstore-root>", options: LoadOptions.PreserveWhitespace);
            wrapped = true;
        }

        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective)
        {
            var maximumDepth = document.Root!.DescendantsAndSelf()
            .Select(selector: element => element.Ancestors()
            .Count() + (wrapped ? 0 : 1))
            .DefaultIfEmpty(defaultValue: 0)
            .Max();

            if (maximumDepth > 128)
            {
                throw new ArgumentException(message: "Selective XML indexes do not support documents deeper than 128 element levels.");
            }

            var selected = new List<(string Path, string Value)>();

            foreach (var path in index.SpecializedOptions.JsonPaths)
            {
                var effective = wrapped && path.StartsWith(value: '/') ? "/sqlstore-root" + (path == "/" ? "" : path) : path;

                if (path == "/")
                {
                    selected.Add(item: (path, document.Root!.Value));
                }
                else
                {
                    foreach (var node in EvaluateXmlNodes(document: document, effectivePath: effective, declaredPath: path, namespaces: index.SpecializedOptions.XmlNamespaces))
                    {
                        selected.Add(item: (path, XmlNodeValue(node: node, path: path)));
                    }
                }
            }

            return selected;
        }

        List<(string Path, string Value)> result = [];

        foreach (var root in wrapped ? document.Root!.Elements() : [document.Root!])
        {
            AddElement(element: root, parentPath: "", output: result);
        }

        return result;

        void AddElement(XElement element, string parentPath, ICollection<(string Path, string Value)> output)
        {
            var path = parentPath + "/" + CanonicalXmlName(name: element.Name);
            output.Add(item: (path, element.Value));

            foreach (var attribute in element.Attributes()
            .Where(predicate: attribute => !attribute.IsNamespaceDeclaration))
            {
                output.Add(item: (path + "/@" + CanonicalXmlName(name: attribute.Name), attribute.Value));
            }

            foreach (var child in element.Nodes())
            {
                switch (child)
                {
                    case XElement childElement:
                        AddElement(element: childElement, parentPath: path, output: output);
                        break;
                    case XCData data:
                        output.Add(item: (path + "/text()", data.Value));
                        break;
                    case XText text:
                        output.Add(item: (path + "/text()", text.Value));
                        break;
                    case XComment comment:
                        output.Add(item: (path + "/comment()", comment.Value));
                        break;
                    case XProcessingInstruction instruction:
                        output.Add(item: (path + "/processing-instruction()", instruction.Data));
                        break;
                }
            }
        }
    }

    private IReadOnlyList<object> EvaluateXmlNodes(XDocument document, string effectivePath, string declaredPath, IReadOnlyDictionary<string, string> namespaces)
    {
        var namespaceManager = new XmlNamespaceManager(nameTable: new NameTable());

        foreach (var binding in namespaces)
        {
            namespaceManager.AddNamespace(prefix: binding.Key, uri: binding.Value);
        }

        var evaluated = document.XPathEvaluate(expression: effectivePath, resolver: namespaceManager);

        if (evaluated is XObject node)
        {
            return[node];
        }

        if (evaluated is not IEnumerable nodes)
        {
            throw new ArgumentException(message: $"Selective XML path '{declaredPath}' must evaluate to nodes.");
        }

        return nodes.Cast<object>()
            .ToArray();
    }

    private string XmlNodeValue(object node, string path) =>
        node switch
    {
        XElement element => element.Value,
        XAttribute attribute => attribute.Value,
        XCData data => data.Value,
        XText text => text.Value,
        XComment comment => comment.Value,
        XProcessingInstruction instruction => instruction.Data,
        _ => throw new ArgumentException(message: $"Selective XML path '{path}' returned an unsupported node kind.")};

    private string CanonicalXmlName(XName name) =>
        name.NamespaceName.Length == 0 ? name.LocalName : "{" + name.NamespaceName + "}" + name.LocalName;

    private string NormalizeXmlLookupPath(CatalogIndex index, string path)
    {
        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective)
        {
            return path;
        }

        if (path == "/")
        {
            return path;
        }

        var segments = path.Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return "/";
        }

        var normalized = new StringBuilder();

        foreach (var rawSegment in segments)
        {
            var attribute = rawSegment.StartsWith(value: '@');
            var segment = attribute ? rawSegment[1..] : rawSegment;

            if (!attribute && segment is "text()" or "comment()" or "processing-instruction()")
            {
                normalized.Append(value: '/')
            .Append(value: segment);

                continue;
            }

            if (segment.Length == 0 || segment.IndexOfAny(anyOf: ['[', ']', '(', ')', '*']) >= 0)
            {
                throw new ArgumentException(message: "Primary XML index lookups require a simple absolute element or attribute path.", paramName: nameof(path));
            }

            var colon = segment.IndexOf(value: ':');
            string localName;
            string? namespaceUri = null;

            if (colon >= 0)
            {
                if (colon == 0 || colon == segment.Length - 1 || segment.IndexOf(value: ':', startIndex: colon + 1) >= 0)
                {
                    throw new ArgumentException(message: "XML lookup path contains an invalid qualified name.", paramName: nameof(path));
                }

                var prefix = segment[..colon];
                localName = segment[(colon + 1)..];

                if (!index.SpecializedOptions.XmlNamespaces.TryGetValue(key: prefix, value: out namespaceUri))
                {
                    throw new ArgumentException(message: $"XML namespace prefix '{prefix}' is not bound by this index.", paramName: nameof(path));
                }
            }
            else
            {
                localName = segment;
            }

            try
            {
                XmlConvert.VerifyNCName(name: localName);
            }
            catch (XmlException exception)
            {
                throw new ArgumentException(message: $"XML path name '{localName}' is invalid.", paramName: nameof(path), innerException: exception);
            }

            normalized.Append(value: '/');

            if (attribute)
            {
                normalized.Append(value: '@');
            }

            if (namespaceUri is not null)
            {
                normalized.Append(value: '{')
            .Append(value: namespaceUri)
            .Append(value: '}');
            }

            normalized.Append(value: localName);
        }

        return normalized.ToString();
    }

    private void ValidateXmlLookupPath(CatalogIndex index, string path)
    {
        if (!path.StartsWith(value: '/'))
        {
            throw new ArgumentException(message: "XML lookup paths must be absolute.", paramName: nameof(path));
        }

        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective && !index.SpecializedOptions.JsonPaths.Contains(value: path, comparer: StringComparer.Ordinal))
        {
            throw new ArgumentException(message: "XML path is not promoted by this selective index.", paramName: nameof(path));
        }
    }

    private void ValidateFor(CatalogTable table, CatalogIndex index)
    {
        if (index.TableId != table.Id)
        {
            throw new ArgumentException(message: "Index does not belong to the table.", paramName: nameof(index));
        }
    }

    private void WriteEscapedBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value == 0)
            {
                WriteByte(output: output, value: 0);
                WriteByte(output: output, value: byte.MaxValue);
            }
            else
            {
                WriteByte(output: output, value: value);
            }
        }

        WriteByte(output: output, value: 0);
        WriteByte(output: output, value: 0);
    }

    private byte[] GetTextSortKey(string value, SqlType type, string? indexCollation)
    {
        value = value.TrimEnd(trimChar: ' ');

        sql_storage_engine.Models.Rows.SqlCollation collation = ParseSqlCollation(name: indexCollation ?? type.Collation ?? ReadDefaultSqlCollation()
            .Name);

        var unicode = type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText;
        return CalculateSqlCollationSortKey(sqlCollation: collation, value: value, unicode: unicode);
    }

    private void WriteSortableInt64(IBufferWriter<byte> output, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(destination: bytes, value: unchecked((ulong)value) ^ 0x8000000000000000UL);
        Write(output: output, value: bytes);
    }

    private void WriteSortableDouble(IBufferWriter<byte> output, double value)
    {
        var bits = BitConverter.DoubleToUInt64Bits(value: value);
        bits = (bits & 0x8000000000000000UL) != 0 ? ~bits : bits ^ 0x8000000000000000UL;
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(destination: bytes, value: bits);
        Write(output: output, value: bytes);
    }

    private void WriteDecimal(IBufferWriter<byte> output, SqlDecimal value)
    {
        TryRescaleSqlDecimal(sqlDecimal: value, scale: 38, coefficient: out var coefficient);
        var magnitude = BigInteger.Abs(value: coefficient);
        Span<byte> bytes = stackalloc byte[33];
        bytes.Clear();
        var negative = coefficient.Sign < 0;
        bytes[0] = negative ? (byte)0x7F : (byte)0x80;
        var magnitudeLength = magnitude.GetByteCount(isUnsigned: true);

        if (!magnitude.TryWriteBytes(destination: bytes[(bytes.Length - magnitudeLength)..], bytesWritten: out _, isUnsigned: true, isBigEndian: true))
        {
            throw new InvalidOperationException(message: "SQL decimal did not fit its canonical index representation.");
        }

        if (negative)
        {
            for (var index = 1; index < bytes.Length; index++)
            {
                bytes[index] ^= 0xFF;
            }
        }

        Write(output: output, value: bytes);
    }

    private void WriteVariant(IBufferWriter<byte> output, SqlValue value, SqlType declaredType)
    {
        switch (GetVariantFamilyOrder(type: declaredType.Name))
        {
            case 1:
                WriteSqlGuid(output: output, value: ((UniqueIdentifierSqlValue)value).Value);
                break;
            case 2:
                WriteEscapedBytes(output: output, bytes: ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)value))
            .Span);
                break;
            case 3:
                sql_storage_engine.Models.Rows.SqlCollation collation = ReadSqlTypeCollationMetadata(sqlType: declaredType) ?? ReadDefaultSqlCollation();
                Span<byte> metadata = stackalloc byte[6];
                BinaryPrimitives.WriteUInt16BigEndian(destination: metadata, value: collation.Lcid);
                metadata[2] = collation.Version;
                BinaryPrimitives.WriteUInt16BigEndian(destination: metadata[3..], value: (ushort)collation.ComparisonFlags);
                metadata[5] = collation.SortId;
                Write(output: output, value: metadata);
                WriteEscapedBytes(output: output, bytes: GetTextSortKey(value: ((TextSqlValue)value).Value, type: declaredType, indexCollation: null));
                break;
            case 4:
                WriteDecimal(output: output, value: value switch
                {
                    BooleanSqlValue boolean => CreateSqlDecimal(coefficient: boolean.Value ? 1 : 0, scale: 0),
                    IntegerSqlValue integer => CreateSqlDecimal(coefficient: integer.Value, scale: 0),
                    DecimalSqlValue exact => exact.Value,
                    _ => throw new ArgumentException(message: "Invalid exact-numeric sql_variant value.", paramName: nameof(value))});
                break;
            case 5:
                WriteSortableDouble(output: output, value: ((FloatSqlValue)value).Value);
                break;
            case 6:
                WriteSortableInt64(output: output, value: value switch
                {
                    DateSqlValue date => date.Value.ToDateTime(time: TimeOnly.MinValue)
            .Ticks,
                    TimeSqlValue time => new DateTime(year: 1900, month: 1, day: 1).Ticks + time.Value.Ticks,
                    DateTimeSqlValue dateTime => dateTime.Value.Ticks,
                    DateTimeOffsetSqlValue offset => offset.Value.UtcTicks,
                    _ => throw new ArgumentException(message: "Invalid temporal sql_variant value.", paramName: nameof(value))});
                break;
            default:
                throw new ArgumentException(message: "Unsupported sql_variant index value.", paramName: nameof(value));
        }
    }

    private byte GetVariantFamilyOrder(SqlTypeName type) =>
        type switch
    {
        SqlTypeName.UniqueIdentifier => 1,
        SqlTypeName.VarBinary or SqlTypeName.Binary => 2,
        SqlTypeName.NVarChar or SqlTypeName.NChar or SqlTypeName.VarChar or SqlTypeName.Char => 3,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney or SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or SqlTypeName.Bit => 4,
        SqlTypeName.Float or SqlTypeName.Real => 5,
        SqlTypeName.DateTimeOffset or SqlTypeName.DateTime2 or SqlTypeName.DateTime or SqlTypeName.SmallDateTime or SqlTypeName.Date or SqlTypeName.Time => 6,
        _ => throw new ArgumentException(message: $"{type} cannot be stored in sql_variant.", paramName: nameof(type))};

    private void WriteSqlGuid(IBufferWriter<byte> output, Guid value)
    {
        Span<byte> source = stackalloc byte[16];
        value.TryWriteBytes(destination: source);
        ReadOnlySpan<byte> order = [10, 11, 12, 13, 14, 15, 8, 9, 7, 6, 5, 4, 3, 2, 1, 0];
        Span<byte> ordered = stackalloc byte[16];

        for (var index = 0; index < ordered.Length; index++)
        {
            ordered[index] = source[order[index]];
        }

        Write(output: output, value: ordered);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value)
    {
        var span = output.GetSpan(sizeHint: 1);
        span[0] = value;
        output.Advance(count: 1);
    }

    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        value.CopyTo(destination: output.GetSpan(sizeHint: value.Length));
        output.Advance(count: value.Length);
    }

    private (int Kind, string Raw, string? Text)[] ReadJsonValues(string json, string path) =>
        ProcessDocument(catalogIndexDocumentContext: new CatalogIndexDocumentContext { Operation = 1, Json = json, Path = path })
            .Values!;

    private string NormalizeJsonPath(string path) =>
        ProcessDocument(catalogIndexDocumentContext: new CatalogIndexDocumentContext { Operation = 3, Path = path })
            .Text!;

    private byte[] ReadUtf8Bytes(string value) =>
        ProcessDocument(catalogIndexDocumentContext: new CatalogIndexDocumentContext { Operation = 4, Text = value })
            .Bytes!;

    private byte[] EncodeJsonObservation(int kind, string raw, string? text) =>
        kind switch
    {
        3 => Tagged(tag: 1, payload: ReadUtf8Bytes(value: text!)),
        4 => Tagged(tag: 2, payload: ReadUtf8Bytes(value: NormalizeJsonNumber(value: raw))),
        5 => [3, 1],
        6 => [3, 0],
        7 => [4],
        1 => Tagged(tag: 5, payload: ReadUtf8Bytes(value: raw)),
        2 => Tagged(tag: 6, payload: ReadUtf8Bytes(value: raw)),
        _ => throw new ArgumentException(message: "Unsupported JSON index value.")};

    private void CheckSqlTypeValue(SqlType sqlType, SqlValue sqlValue, string columnName) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 9, SqlType = sqlType, SqlValue = sqlValue, ColumnName = columnName });

    private SqlValue NormalizeSqlTypeValue(SqlType sqlType, SqlValue sqlValue) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 10, SqlType = sqlType, SqlValue = sqlValue })
            .SqlValue!;

    private bool ReadSqlTypeCanBeBTreeKey(SqlType sqlType) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 11, SqlType = sqlType })
            .BooleanResult;

    private string FormatSqlType(SqlType sqlType) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 12, SqlType = sqlType })
            .Text!;

    private SqlCollation? ReadSqlTypeCollationMetadata(SqlType sqlType) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 13, SqlType = sqlType })
            .Collation!;

    private byte[] EncodeSqlSpatial(sql_storage_engine.Models.Rows.SqlSpatial sqlSpatial) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 14, Spatial = sqlSpatial })
            .Bytes!;

    private (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBounds(sql_storage_engine.Models.Rows.SqlSpatial sqlSpatial) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 15, Spatial = sqlSpatial })
            .Bounds;

    private SqlCollation ParseSqlCollation(string name) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 2, Text = name })
            .Collation!;

    private SqlCollation ReadDefaultSqlCollation() =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 3 })
            .Collation!;

    private byte[] CalculateSqlCollationSortKey(SqlCollation sqlCollation, string value, bool unicode) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 4, Collation = sqlCollation, Text = value, Unicode = unicode })
            .Bytes!;

    private IReadOnlyList<string> TokenizeCatalogFullTextIndexOptionsDocument(string document, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 6, Text = document, FullTextOptions = catalogFullTextIndexOptions })
            .Tokens!;

    private string ParseCatalogFullTextIndexOptionsExactTerm(string term, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 7, Text = term, FullTextOptions = catalogFullTextIndexOptions })
            .Text!;

    private byte[] Encrypt(CatalogColumnEncryption metadata, ReadOnlySpan<byte> plaintext) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 5, Encryption = metadata, Bytes = plaintext.ToArray() })
            .Bytes!;

    private CatalogSpecializedIndexOptions CreateJsonCatalogSpecializedIndexOptions(params string[] paths) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 8, SpecializedOptions = new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Json, PathSource = paths } })
            .SpecializedOptions!;

    private CatalogIndexDocumentContext ProcessDocument(CatalogIndexDocumentContext catalogIndexDocumentContext) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 1, Document = catalogIndexDocumentContext })
            .Document!;

    private byte[] EncodeRow(Row row, TableDefinition table) =>
        EncodeRowEncodingContext(rowEncodingContext: new RowEncodingContext { Row = row, Table = table })
            .Bytes!;

    private Row DecodeRow(ReadOnlySpan<byte> source, TableDefinition table) =>
        DecodeRowEncodingContext(rowEncodingContext: new RowEncodingContext { Source = source.ToArray(), Table = table })
            .Row!;

    private byte[] EncodeLogicalValue(SqlValue value, ColumnDefinition column) =>
        EncodeLogicalRowEncodingContext(rowEncodingContext: new RowEncodingContext { Value = value, Column = column })
            .Bytes!;

    private static Row CreateRow(IEnumerable<SqlValue> values) =>
        new Row
    {
        Values = Array.AsReadOnly(array: values.ToArray())
    };

    private TableDefinition CreateTableDefinition(IEnumerable<ColumnDefinition> columnDefinitions) =>
        projections.ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 17, TableDefinition = new TableDefinition { ColumnSource = columnDefinitions } })
            .TableDefinition!;

    private static ColumnDefinition CreateColumnDefinition(sql_storage_engine.Models.Identifiers.ColumnId columnId, string name, SqlType sqlType, bool isNullable, CatalogColumnEncryption? catalogColumnEncryption = null, bool isSparse = false, bool isColumnSet = false) =>
        new ColumnDefinition
    {
        Id = columnId,
        Name = name,
        Type = sqlType,
        IsNullable = isNullable,
        Encryption = catalogColumnEncryption,
        IsSparse = isSparse,
        IsColumnSet = isColumnSet
    };

    private static sql_storage_engine.Models.Indexes.IndexKey CreateIndexKey(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException(message: "Index keys must contain between 1 and 65,535 bytes.");
        }

        var chars = new char[bytes.Length];

        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = (char)bytes[i];
        }

        return new sql_storage_engine.Models.Indexes.IndexKey
        {
            Content = new string (value: chars)
        };
    }

    private static ReadOnlyMemory<byte> ReadBinarySqlValue(BinarySqlValue binarySqlValue) =>
        binarySqlValue.Content.Select(selector: item => checked((byte)item))
            .ToArray();

    private static SqlDecimal CreateSqlDecimal(BigInteger coefficient, byte scale) =>
        new SqlDecimal
    {
        Coefficient = coefficient,
        Scale = scale
    };

    private static string FormatSqlDecimal(SqlDecimal sqlDecimal)
    {
        var digits = BigInteger.Abs(value: sqlDecimal.Coefficient)
            .ToString(provider: System.Globalization.CultureInfo.InvariantCulture)
            .PadLeft(totalWidth: sqlDecimal.Scale + 1, paddingChar: '0');

        if (sqlDecimal.Scale > 0)
        {
            digits = digits.Insert(startIndex: digits.Length - sqlDecimal.Scale, value: ".");
        }

        return sqlDecimal.Coefficient.Sign < 0 ? "-" + digits : digits;
    }

    private static bool TryRescaleSqlDecimal(SqlDecimal sqlDecimal, byte scale, out BigInteger coefficient)
    {
        if (scale >= sqlDecimal.Scale)
        {
            coefficient = sqlDecimal.Coefficient * BigInteger.Pow(value: 10, exponent: scale - sqlDecimal.Scale);
            return true;
        }

        coefficient = BigInteger.DivRem(dividend: sqlDecimal.Coefficient, divisor: BigInteger.Pow(value: 10, exponent: sqlDecimal.Scale - scale), remainder: out var remainder);
        return remainder.IsZero;
    }

    private static float[] ReadVector(VectorSqlValue vectorSqlValue)
    {
        if (vectorSqlValue.BaseType == SqlVectorBaseType.Float16)
        {
            return ReadHalfVector(vectorSqlValue: vectorSqlValue)
            .Select(selector: value => (float)value)
            .ToArray();
        }

        var content = vectorSqlValue.Content;
        var values = new float[content.Length / 2];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BitConverter.UInt32BitsToSingle(value: (uint)content[i * 2] | ((uint)content[i * 2 + 1] << 16));
        }

        return values;
    }

    private static Half[] ReadHalfVector(VectorSqlValue vectorSqlValue) =>
        vectorSqlValue.BaseType == SqlVectorBaseType.Float16 ? vectorSqlValue.Content.Select(selector: item => BitConverter.UInt16BitsToHalf(value: item))
            .ToArray() : [];

    private static BigInteger[][] ParseLabels(string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value == "/")
        {
            return[];
        }

        if (value.Length < 3 || value[0] != '/' || value[^1] != '/')
        {
            throw new FormatException(message: "A hierarchyid path must begin and end with '/'.");
        }

        var parts = value[1..^1].Split(separator: '/');
        var labels = new BigInteger[parts.Length][];

        for (var level = 0; level < parts.Length; level++)
        {
            var components = parts[level].Split(separator: '.');

            if (components.Length == 0)
            {
                throw new FormatException(message: "A hierarchyid label cannot be empty.");
            }

            labels[level] = new BigInteger[components.Length];

            for (var component = 0; component < components.Length; component++)
            {
                var token = components[component];

                if (!BigInteger.TryParse(value: token, style: System.Globalization.NumberStyles.AllowLeadingSign, provider: System.Globalization.CultureInfo.InvariantCulture, result: out labels[level][component]) || !StringComparer.Ordinal.Equals(x: token, y: labels[level][component].ToString(provider: System.Globalization.CultureInfo.InvariantCulture)))
                {
                    throw new FormatException(message: $"Invalid hierarchyid integer '{token}'.");
                }
            }
        }

        return labels;
    }

    private static void WriteInteger(IBufferWriter<byte> output, BigInteger value)
    {
        WriteByte(output: output, value: value.Sign switch
        {
            < 0 => (byte)0,
            0 => (byte)1,
            _ => (byte)2
        });

        var magnitude = BigInteger.Abs(value: value)
            .ToByteArray(isUnsigned: true, isBigEndian: true);

        WriteVarUInt(output: output, value: checked((uint)magnitude.Length));
        Write(output: output, value: magnitude);
    }

    private static void WriteSortableInteger(IBufferWriter<byte> output, BigInteger value)
    {
        var magnitude = BigInteger.Abs(value: value)
            .ToByteArray(isUnsigned: true, isBigEndian: true);

        if (magnitude.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException(message: "Hierarchyid integer is too large.");
        }

        Span<byte> length = stackalloc byte[2];

        if (value.Sign < 0)
        {
            WriteByte(output: output, value: 0x10);
            BinaryPrimitives.WriteUInt16BigEndian(destination: length, value: checked((ushort)(ushort.MaxValue - magnitude.Length)));
            Write(output: output, value: length);

            foreach (var item in magnitude)
            {
                WriteByte(output: output, value: (byte)~item);
            }
        }
        else
        {
            WriteByte(output: output, value: 0x20);
            BinaryPrimitives.WriteUInt16BigEndian(destination: length, value: checked((ushort)magnitude.Length));
            Write(output: output, value: length);
            Write(output: output, value: magnitude);
        }
    }

    private static void WriteVarUInt(IBufferWriter<byte> output, uint value)
    {
        while (value >= 0x80)
        {
            WriteByte(output: output, value: (byte)(value | 0x80));
            value >>= 7;
        }

        WriteByte(output: output, value: (byte)value);
    }

    private byte[] WriteSqlHierarchyId(SqlHierarchyId sqlHierarchyId)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId);
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId.Path);
        var output = new ArrayBufferWriter<byte>();
        var labels = ParseLabels(value: sqlHierarchyId.Path);
        WriteByte(output: output, value: 2);
        WriteVarUInt(output: output, value: checked((uint)labels.Length));

        foreach (var label in labels)
        {
            WriteVarUInt(output: output, value: checked((uint)label.Length));

            foreach (var component in label)
            {
                WriteInteger(output: output, value: component);
            }
        }

        if (output.WrittenCount > 892)
        {
            throw new InvalidOperationException(message: "The hierarchyid representation exceeds 892 bytes.");
        }

        return output.WrittenSpan.ToArray();
    }

    private byte[] ReadSqlHierarchyIdSortKey(SqlHierarchyId sqlHierarchyId)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId);
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId.Path);
        var output = new ArrayBufferWriter<byte>();

        foreach (var label in ParseLabels(value: sqlHierarchyId.Path))
        {
            foreach (var component in label)
            {
                WriteSortableInteger(output: output, value: component);
            }

            WriteByte(output: output, value: 0);
        }

        return output.WrittenSpan.ToArray();
    }

    private int CountSqlCollationBytes(SqlCollation sqlCollation, string value) =>
        ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 16, Collation = sqlCollation, Text = value })
            .NumberResult;

    private CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);

        switch (catalogIndexProjectionContext.Operation)
        {
            case 17:
            case 18:
            case 16:
            case >= 1 and <= 8:
                return projections.ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: catalogIndexProjectionContext);
            case 9:
                types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexProjectionContext.SqlType, InputSqlValueValue = catalogIndexProjectionContext.SqlValue, InputstringColumnName = catalogIndexProjectionContext.ColumnName });
                break;
            case 10:
                catalogIndexProjectionContext.SqlValue = types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexProjectionContext.SqlType, InputSqlValueValue = catalogIndexProjectionContext.SqlValue })
            .SqlValueResult;
                break;
            case 11:
                catalogIndexProjectionContext.BooleanResult = types.ProcessSqlTypeContextCanBeBTreeKey5(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexProjectionContext.SqlType })
            .BooleanResult;
                break;
            case 12:
                catalogIndexProjectionContext.Text = types.ProcessSqlTypeContextToString80(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexProjectionContext.SqlType })
            .TextResult;
                break;
            case 13:
                catalogIndexProjectionContext.Collation = types.ProcessSqlTypeContextCollationMetadata0(sqlTypeContext: new SqlTypeContext { SqlTypeSource = catalogIndexProjectionContext.SqlType })
            .SqlCollationResult;
                break;
            case 14:
                catalogIndexProjectionContext.Bytes = formats.ProcessRowEncodingContextSpatialEncode(rowEncodingContext: new RowEncodingContext { Value = new SpatialSqlValue { Value = catalogIndexProjectionContext.Spatial!, StorageFamily = SqlStorageFamily.Binary } })
            .Bytes!;
                break;
            case 15:
                catalogIndexProjectionContext.Bounds = formats.ProcessRowEncodingContextSpatialBounds(rowEncodingContext: new RowEncodingContext { Value = new SpatialSqlValue { Value = catalogIndexProjectionContext.Spatial!, StorageFamily = SqlStorageFamily.Binary } })
            .Bounds;
                break;
        }

        return catalogIndexProjectionContext;
    });

    private RowEncodingContext EncodeRowEncodingContext(RowEncodingContext rowEncodingContext)
    {
        CheckRowMetadata(rowEncodingContext: rowEncodingContext);
        var values = rowEncodingContext.Row!.Values.ToArray();

        for (var index = 0; index < values.Length; index++)
        {
            var column = rowEncodingContext.Table!.Columns[index];
            ValidateTypedValue(column: column, value: values[index]);
            values[index] = NormalizeTypedValue(column: column, value: values[index]);
        }

        rowEncodingContext.Row = new Row
        {
            Values = values
        };

        return formats.ProcessRowEncodingContextEncode(rowEncodingContext: rowEncodingContext);
    }

    private RowEncodingContext DecodeRowEncodingContext(RowEncodingContext rowEncodingContext)
    {
        formats.ProcessRowEncodingContextDecode(rowEncodingContext: rowEncodingContext);

        try
        {
            rowEncodingContext.Row = new Row
            {
                Values = rowEncodingContext.Row!.Values.Select(selector: MaterializeDecodedValue)
            .ToArray()
            };

            CheckRowMetadata(rowEncodingContext: rowEncodingContext);

            for (var index = 0; index < rowEncodingContext.Table!.Columns.Count; index++)
            {
                ValidateTypedValue(column: rowEncodingContext.Table.Columns[index], value: rowEncodingContext.Row.Values[index]);
            }
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Decoded row violates its declared schema.", innerException: exception);
        }

        return rowEncodingContext;
    }

    private RowEncodingContext EncodeLogicalRowEncodingContext(RowEncodingContext rowEncodingContext)
    {
        ValidateTypedValue(column: rowEncodingContext.Column!, value: rowEncodingContext.Value!);
        rowEncodingContext.Value = NormalizeTypedValue(column: rowEncodingContext.Column!, value: rowEncodingContext.Value!);
        return formats.ProcessRowEncodingContextLogicalEncode(rowEncodingContext: rowEncodingContext);
    }

    private void CheckRowMetadata(RowEncodingContext rowEncodingContext) =>
        projections.ProcessCatalogIndexProjectionContext(catalogIndexProjectionContext: new CatalogIndexProjectionContext { Operation = 18, TableDefinition = new TableDefinition { SchemaSource = rowEncodingContext.Table, RowInput = rowEncodingContext.Row } });

    private void ValidateTypedValue(ColumnDefinition column, SqlValue value)
    {
        if (value.IsNull && !column.IsNullable)
        {
            throw new ArgumentException(message: $"Column '{column.Name}' does not allow SQL NULL.");
        }

        types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = column.Type, InputSqlValueValue = value, InputstringColumnName = column.Name });
    }

    private SqlValue NormalizeTypedValue(ColumnDefinition column, SqlValue value) =>
        types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = column.Type, InputSqlValueValue = value })
            .SqlValueResult!;

    private SqlValue MaterializeDecodedValue(SqlValue value)
    {
        if (value is not VariantSqlValue variant)
        {
            return value;
        }

        var type = variant.DeclaredType;

        var restored = types.ProcessSqlTypeContextRestore73(sqlTypeContext: new SqlTypeContext { InputSqlTypeNameName = type.Name, InputbytePrecision = type.Precision, InputbyteScale = type.Scale, InputintLength = type.Length, InputboolIsMax = type.IsMax, InputstringCollation = type.Collation, InputushortVectorDimensions = type.VectorDimensions, InputSqlVectorBaseTypeVectorBaseType = type.VectorBaseType })
            .SqlTypeResult!;

        var column = new ColumnDefinition
        {
            Name = "sql_variant",
            Type = restored,
            IsNullable = false
        };

        ValidateTypedValue(column: column, value: variant.Value);

        return variant with
        {
            DeclaredType = restored,
            Value = NormalizeTypedValue(column: column, value: variant.Value)
        };
    }

    internal static CatalogIndexKeyRequestManagementService CreateCatalogIndexKeyRequestManagementService() =>
        new CatalogIndexKeyRequestManagementService(projections: sql_storage_engine.Services.Coordinations.CatalogIndexProjections.CatalogIndexProjectionContextCoordinationService.CreateCatalogIndexProjectionContextCoordinationService(), types: sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), formats: sql_storage_engine.Services.Coordinations.RowFormats.RowFormatContextCoordinationService.CreateRowFormatContextCoordinationService());
}
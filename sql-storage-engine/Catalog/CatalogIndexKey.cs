using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Catalog;

/// <summary>Builds deterministic composite keys from catalog ordering metadata.</summary>
public static class CatalogIndexKey
{
    public const int MaximumClusteredKeyBytes = 900;
    public const int MaximumNonClusteredKeyBytes = 1_700;
    public const int MaximumSqlVariantKeyBytes = 900;

    /// <summary>Returns every durable tree entry represented by one row.</summary>
    public static IReadOnlyList<IndexKey> EncodeEntries(Row row, CatalogTable table, CatalogIndex index)
    {
        if (index.Method == CatalogIndexMethod.Spatial &&
            index.SpecializedOptions!.SpatialSrid is { } requiredSrid)
        {
            var spatialValue = SourceValue(row, table, index);
            if (spatialValue.IsNull || ((SpatialSqlValue)spatialValue).Value.Srid != requiredSrid) return [];
        }
        var documentKey = Encode(row, table, index);
        if (index.Method is not (CatalogIndexMethod.Json or CatalogIndexMethod.Xml)) return [documentKey];
        var value = SourceValue(row, table, index);
        if (value.IsNull) return [documentKey];
        var entries = new HashSet<IndexKey> { documentKey };
        if (index.Method == CatalogIndexMethod.Json)
        {
            using var document = JsonDocument.Parse(((TextSqlValue)value).Value,
                new JsonDocumentOptions { MaxDepth = 128 });
            for (var ordinal = 0; ordinal < index.SpecializedOptions!.JsonPaths.Count; ordinal++)
            {
                var promotedPath = index.SpecializedOptions.JsonPaths[ordinal];
                var selected = SelectJsonPath(document.RootElement, promotedPath);
                if (selected.Count != 0) entries.Add(EncodePathExists(3, ordinal));
                foreach (var item in selected) entries.Add(EncodePathValue(2, ordinal, EncodeJsonValue(item)));
                var normalizedPath = NormalizeJsonPath(promotedPath);
                foreach (var item in selected) AddJsonDescendantEntries(item, normalizedPath, entries);
            }
        }
        else
        {
            var nodes = SelectXmlNodes(((TextSqlValue)value).Value, index);
            foreach (var node in nodes)
            {
                entries.Add(EncodeNamedPathExists(3, node.Path));
                entries.Add(EncodeNamedPathValue(2, node.Path, RowCodec.Utf8.GetBytes(node.Value)));
            }
        }
        return entries.OrderBy(key => key).ToArray();
    }

    public static IndexKey EncodeJsonPathValue(CatalogIndex index, string path, SqlValue value)
    {
        var keys = EncodeJsonPathValueKeys(index, path, value);
        if (keys.Count != 1)
            throw new ArgumentException("The JSON path expands to multiple index seeks; use IStorageIndex.FindJsonPathAsync.", nameof(path));
        return keys[0];
    }

    internal static IReadOnlyList<IndexKey> EncodeJsonPathValueKeys(CatalogIndex index, string path, SqlValue value)
    {
        if (index.Method != CatalogIndexMethod.Json) throw new ArgumentException("Index is not a JSON index.", nameof(index));
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(value);
        var (ordinal, normalized, exact) = ResolveJsonLookupPath(index, path);
        if (exact) return [EncodePathValue(2, ordinal, EncodeJsonValue(value))];
        var encodedValue = EncodeJsonValue(value);
        return ExpandJsonLookupPaths(normalized).Select(candidate =>
            EncodeNamedPathValue(4, candidate, encodedValue)).ToArray();
    }

    public static IndexKey EncodeJsonPathExists(CatalogIndex index, string path)
    {
        var keys = EncodeJsonPathExistsKeys(index, path);
        if (keys.Count != 1)
            throw new ArgumentException("The JSON path expands to multiple index seeks; use IStorageIndex.FindJsonPathExistsAsync.", nameof(path));
        return keys[0];
    }

    internal static IReadOnlyList<IndexKey> EncodeJsonPathExistsKeys(CatalogIndex index, string path)
    {
        if (index.Method != CatalogIndexMethod.Json) throw new ArgumentException("Index is not a JSON index.", nameof(index));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var (ordinal, normalized, exact) = ResolveJsonLookupPath(index, path);
        if (exact) return [EncodePathExists(3, ordinal)];
        return ExpandJsonLookupPaths(normalized).Select(candidate => EncodeNamedPathExists(5, candidate)).ToArray();
    }

    public static IndexKey EncodeXmlPathValue(CatalogIndex index, string path, string value)
    {
        if (index.Method != CatalogIndexMethod.Xml) throw new ArgumentException("Index is not an XML index.", nameof(index));
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(value);
        ValidateXmlLookupPath(index, path);
        return EncodeNamedPathValue(2, NormalizeXmlLookupPath(index, path), RowCodec.Utf8.GetBytes(value));
    }

    public static IndexKey EncodeXmlPathExists(CatalogIndex index, string path)
    {
        if (index.Method != CatalogIndexMethod.Xml) throw new ArgumentException("Index is not an XML index.", nameof(index));
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ValidateXmlLookupPath(index, path);
        return EncodeNamedPathExists(3, NormalizeXmlLookupPath(index, path));
    }
    public static IndexKey Encode(Row row, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        if (index.TableId != table.Id) throw new ArgumentException("Index does not belong to the table.", nameof(index));
        table.ValidateFor(index);
        if (row.Values.Count != table.Columns.Count)
            throw new ArgumentException("Row width does not match the table definition.", nameof(row));
        return EncodeValues(index.Columns.Select(indexed => row.Values[table.Columns.Select((column, offset) => (column, offset))
            .Single(item => item.column.Id == indexed.ColumnId).offset]).ToArray(), table, index);
    }

    private static SqlValue SourceValue(Row row, CatalogTable table, CatalogIndex index)
    {
        if (row.Values.Count != table.Columns.Count) throw new ArgumentException("Row width does not match the table definition.", nameof(row));
        var columnId = index.Columns.Single().ColumnId;
        var position = table.Columns.Select((column, offset) => (column, offset))
            .Single(item => item.column.Id == columnId).offset;
        return row.Values[position];
    }

    /// <summary>Encodes declared INCLUDE columns into a leaf payload, avoiding a heap fetch for covered projections.</summary>
    public static byte[] EncodeIncludedValues(Row row, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        if (index.IncludedColumns.Count == 0) return [];
        if (row.Values.Count != table.Columns.Count)
            throw new ArgumentException("Row width does not match the table definition.", nameof(row));
        var columns = IncludedColumns(table, index);
        var positions = columns.Select(column => table.Columns.Select((candidate, position) => (candidate, position))
            .Single(item => item.candidate.Id == column.Id).position).ToArray();
        var schema = new TableDefinition(columns.Select(column => new ColumnDefinition(
            column.Id, column.Name, column.Type, column.IsNullable, column.Encryption,
            column.IsSparse, column.IsColumnSet)));
        var encoded = RowCodec.Encode(new Row(positions.Select(position => row.Values[position])), schema);
        if (encoded.Length > ushort.MaxValue)
            throw new StorageResourceExhaustedException("Covered index payload exceeds 65,535 bytes.");
        return encoded;
    }

    public static IReadOnlyDictionary<ColumnId, SqlValue> DecodeIncludedValues(ReadOnlySpan<byte> payload,
        CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        if (index.IncludedColumns.Count == 0)
        {
            if (!payload.IsEmpty) throw new StorageFormatException("Index without INCLUDE columns has a covered payload.");
            return new Dictionary<ColumnId, SqlValue>();
        }
        if (payload.IsEmpty) throw new StorageCorruptionException("Covered index entry has no included payload.");
        var columns = IncludedColumns(table, index);
        var schema = new TableDefinition(columns.Select(column => new ColumnDefinition(
            column.Id, column.Name, column.Type, column.IsNullable, column.Encryption,
            column.IsSparse, column.IsColumnSet)));
        var row = RowCodec.Decode(payload, schema);
        return columns.Select((column, position) => (column.Id, Value: row.Values[position]))
            .ToDictionary(item => item.Id, item => item.Value);
    }

    private static IReadOnlyList<CatalogColumn> IncludedColumns(CatalogTable table, CatalogIndex index)
    {
        table.ValidateFor(index);
        return index.IncludedColumns.Select(id => table.Columns.Single(column => column.Id == id)).ToArray();
    }

    /// <summary>Encodes values in index-column order for exact lookup and bounded index scans.</summary>
    public static IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index)
        => EncodeValuesCore(values, table, index, null, allowPrefix: false);

    /// <summary>Encodes a non-empty leading subset of a composite B-tree key.</summary>
    public static IndexKey EncodePrefix(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index)
        => EncodeValuesCore(values, table, index, null, allowPrefix: true);

    internal static IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index,
        int maximumKeyBytes) => EncodeValuesCore(values, table, index, maximumKeyBytes, allowPrefix: false);

    private static IndexKey EncodeValuesCore(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index,
        int? maximumKeyBytesOverride, bool allowPrefix)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        table.ValidateFor(index);
        if ((!allowPrefix && values.Count != index.Columns.Count) ||
            (allowPrefix && (values.Count == 0 || values.Count > index.Columns.Count)))
            throw new ArgumentException($"Expected {index.Columns.Count} index values, received {values.Count}.", nameof(values));
        if (allowPrefix && index.Method != CatalogIndexMethod.BTree)
            throw new ArgumentException("Prefix bounds apply only to B-tree indexes.", nameof(index));
        if (index.Method != CatalogIndexMethod.BTree) return EncodeSpecialized(values.Single(), table, index);
        var output = new ArrayBufferWriter<byte>();
        var logicalKeyBytes = 0;
        for (var indexOffset = 0; indexOffset < values.Count; indexOffset++)
        {
            var indexed = index.Columns[indexOffset];
            var column = table.Columns.Single(candidate => candidate.Id == indexed.ColumnId);
            if (!column.Type.CanBeBTreeKey)
                throw new ArgumentException($"SQL Server type {column.Type} cannot be a B-tree key.", nameof(index));
            var value = values[indexOffset] ?? throw new ArgumentException("Index values cannot contain null CLR references.", nameof(values));
            if (!value.IsNull) column.Type.Validate(value, column.Name);
            value = column.Type.NormalizeForStorage(value);
            if (column.Type.UserTypeKind == SqlUserTypeKind.Clr && value is BinarySqlValue clr &&
                clr.Value.Length > MaximumNonClusteredKeyBytes)
                throw new ArgumentException("CLR UDT index value exceeds the nonclustered key limit.", nameof(values));
            if (value is VariantSqlValue variantValue && GetVariantPayloadLength(variantValue) > MaximumSqlVariantKeyBytes)
                throw new ArgumentException($"sql_variant index value exceeds {MaximumSqlVariantKeyBytes} bytes.", nameof(values));
            logicalKeyBytes = checked(logicalKeyBytes + GetLogicalKeyBytes(column.Type, value));
            var segment = new ArrayBufferWriter<byte>();
            if (value.IsNull)
                WriteByte(segment, indexed.NullSortOrder == NullSortOrder.First ? (byte)0 : (byte)255);
            else
            {
                WriteByte(segment, indexed.NullSortOrder == NullSortOrder.First ? (byte)1 : (byte)254);
                if (column.Encryption is { } encryption)
                {
                    var logicalColumn = new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable);
                    WriteEscapedBytes(segment, SqlColumnEncryption.Encrypt(encryption,
                        RowCodec.EncodeLogicalValue(value, logicalColumn)));
                }
                else
                switch (value)
                {
                    case BooleanSqlValue boolean: WriteByte(segment, boolean.Value ? (byte)1 : (byte)0); break;
                    case IntegerSqlValue integer:
                        var integerBytes = new byte[8];
                        BinaryPrimitives.WriteUInt64BigEndian(integerBytes, unchecked((ulong)integer.Value) ^ 0x8000000000000000UL);
                        Write(segment, integerBytes);
                        break;
                    case TextSqlValue text: WriteEscapedBytes(segment, GetTextSortKey(text.Value, column.Type,
                        indexed.Collation)); break;
                    case BinarySqlValue binary: WriteEscapedBytes(segment, binary.Value.Span); break;
                    case HierarchyIdSqlValue hierarchy: WriteEscapedBytes(segment, hierarchy.Value.GetSortKey()); break;
                    case DecimalSqlValue exact: WriteDecimal(segment, exact.Value); break;
                    case FloatSqlValue approximate: WriteSortableDouble(segment, approximate.Value); break;
                    case DateSqlValue date: WriteSortableInt64(segment, date.Value.DayNumber); break;
                    case TimeSqlValue time: WriteSortableInt64(segment, time.Value.Ticks); break;
                    case DateTimeSqlValue dateTime: WriteSortableInt64(segment, dateTime.Value.Ticks); break;
                    case DateTimeOffsetSqlValue dateTimeOffset: WriteSortableInt64(segment, dateTimeOffset.Value.UtcTicks); break;
                    case UniqueIdentifierSqlValue identifier:
                        WriteSqlGuid(segment, identifier.Value);
                        break;
                    case VariantSqlValue variant:
                        WriteByte(segment, GetVariantFamilyOrder(variant.DeclaredType.Name));
                        WriteVariant(segment, variant.Value, variant.DeclaredType);
                        break;
                    default: throw new ArgumentException("Unsupported indexed SQL value.", nameof(values));
                }
            }
            var encodedSegment = segment.WrittenSpan.ToArray();
            if (indexed.Direction == SortDirection.Descending)
                for (var offset = 1; offset < encodedSegment.Length; offset++) encodedSegment[offset] ^= 0xFF;
            Write(output, encodedSegment);
        }
        var maximumKeyBytes = maximumKeyBytesOverride ?? (index.StorageKind == CatalogIndexStorageKind.Clustered
            ? MaximumClusteredKeyBytes : MaximumNonClusteredKeyBytes);
        if (logicalKeyBytes > maximumKeyBytes)
            throw new ArgumentException($"Actual index key is {logicalKeyBytes} bytes and exceeds the {maximumKeyBytes}-byte " +
                                        $"{index.StorageKind.ToString().ToLowerInvariant()} key limit.", nameof(values));
        return new IndexKey(output.WrittenSpan);
    }

    private static int GetLogicalKeyBytes(SqlType type, SqlValue value)
    {
        if (value.IsNull) return 0;
        if (type.UserTypeKind == SqlUserTypeKind.Alias) return GetLogicalKeyBytes(type.BaseType!, value);
        if (type.UserTypeKind == SqlUserTypeKind.Clr) return ((BinarySqlValue)value).Value.Length;
        return type.Name switch
        {
            SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
            SqlTypeName.SmallInt => 2,
            SqlTypeName.Int or SqlTypeName.Real or SqlTypeName.SmallMoney or SqlTypeName.SmallDateTime => 4,
            SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.DateTime => 8,
            SqlTypeName.Float => type.Precision <= 24 ? 4 : 8,
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 :
                type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.Date => 3,
            SqlTypeName.Time => type.Scale <= 2 ? 3 : type.Scale <= 4 ? 4 : 5,
            SqlTypeName.DateTime2 => type.Scale <= 2 ? 6 : type.Scale <= 4 ? 7 : 8,
            SqlTypeName.DateTimeOffset => type.Scale <= 2 ? 8 : type.Scale <= 4 ? 9 : 10,
            SqlTypeName.Char or SqlTypeName.VarChar =>
                (type.CollationMetadata ?? SqlCollation.Default).GetByteCount(((TextSqlValue)value).Value),
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(((TextSqlValue)value).Value.Length * 2),
            SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.RowVersion or SqlTypeName.Timestamp =>
                ((BinarySqlValue)value).Value.Length,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.HierarchyId => ((HierarchyIdSqlValue)value).Value.Serialize().Length,
            SqlTypeName.SqlVariant => checked(GetVariantPayloadLength((VariantSqlValue)value) + 16),
            _ => throw new ArgumentException($"SQL Server type {type} cannot be measured as a B-tree key.", nameof(type))
        };
    }

    private static int GetVariantPayloadLength(VariantSqlValue variant) => variant.Value switch
    {
        TextSqlValue text when variant.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar => checked(text.Value.Length * 2),
        TextSqlValue text => (variant.DeclaredType.CollationMetadata ?? SqlCollation.Default).GetByteCount(text.Value),
        BinarySqlValue binary => binary.Value.Length,
        _ => 32
    };

    private static IndexKey EncodeSpecialized(SqlValue value, CatalogTable table, CatalogIndex index)
    {
        var indexed = index.Columns.Single();
        var column = table.Columns.Single(candidate => candidate.Id == indexed.ColumnId);
        if (value.IsNull) return new IndexKey([0]);
        column.Type.Validate(value, column.Name);
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, 1);
        switch (index.Method)
        {
            case CatalogIndexMethod.Json:
                using (var document = JsonDocument.Parse(((TextSqlValue)value).Value,
                           new JsonDocumentOptions { MaxDepth = 128 }))
                    foreach (var path in index.SpecializedOptions!.JsonPaths)
                    {
                        var selected = SelectJsonPath(document.RootElement, path);
                        WriteSortableInt64(output, selected.Count);
                        foreach (var item in selected) WriteEscapedBytes(output, RowCodec.Utf8.GetBytes(item.GetRawText()));
                    }
                break;
            case CatalogIndexMethod.Spatial:
                var spatial = ((SpatialSqlValue)value).Value; var bounds = spatial.Bounds;
                WriteSortableDouble(output, bounds.MinX); WriteSortableDouble(output, bounds.MinY);
                WriteSortableDouble(output, bounds.MaxX); WriteSortableDouble(output, bounds.MaxY);
                WriteEscapedBytes(output, spatial.Serialize());
                break;
            case CatalogIndexMethod.Vector:
                var vector = (VectorSqlValue)value;
                WriteByte(output, (byte)vector.BaseType);
                Span<byte> componentBytes = stackalloc byte[4];
                if (vector.BaseType == SqlVectorBaseType.Float32)
                    foreach (var component in vector.Values)
                    { BinaryPrimitives.WriteSingleBigEndian(componentBytes, component); Write(output, componentBytes); }
                else
                    foreach (var component in vector.HalfValues)
                    { BinaryPrimitives.WriteHalfBigEndian(componentBytes, component); Write(output, componentBytes[..2]); }
                break;
            case CatalogIndexMethod.Xml:
                var xml = ((TextSqlValue)value).Value;
                XDocument xmlDocument; var wrapped = false;
                try { xmlDocument = XDocument.Parse(xml, LoadOptions.PreserveWhitespace); }
                catch (System.Xml.XmlException) { xmlDocument = XDocument.Parse("<sqlstore-root>" + xml + "</sqlstore-root>", LoadOptions.PreserveWhitespace); wrapped = true; }
                foreach (var path in index.SpecializedOptions!.JsonPaths)
                {
                    var effectivePath = wrapped && path.StartsWith('/') ? "/sqlstore-root" + (path == "/" ? "" : path) : path;
                    var selected = path == "/" ? [xmlDocument.Root!] : EvaluateXmlNodes(xmlDocument, effectivePath, path,
                        index.SpecializedOptions.XmlNamespaces);
                    WriteSortableInt64(output, selected.Count);
                    foreach (var node in selected)
                    {
                        var text = node is XElement element
                            ? element.ToString(SaveOptions.DisableFormatting)
                            : XmlNodeValue(node, path);
                        WriteEscapedBytes(output, RowCodec.Utf8.GetBytes(text));
                    }
                }
                break;
            default: throw new ArgumentException("Unknown specialized index method.", nameof(index));
        }
        return new IndexKey(output.WrittenSpan);
    }

    private static IReadOnlyList<JsonElement> SelectJsonPath(JsonElement root, string path)
    {
        List<JsonElement> values = [root]; if (path == "$") return values;
        var position = 1;
        while (position < path.Length)
        {
            if (path[position] == '.')
            {
                position++;
                if (position < path.Length && path[position] == '*')
                { position++; values = values.Where(value => value.ValueKind == JsonValueKind.Object).SelectMany(value => value.EnumerateObject().Select(property => property.Value)).ToList(); continue; }
                string property;
                if (position < path.Length && path[position] == '"')
                {
                    var literalStart = position++;
                    while (position < path.Length && path[position] != '"')
                    { if (path[position] == '\\' && position + 1 < path.Length) position++; position++; }
                    if (position >= path.Length) throw new ArgumentException($"JSON path '{path}' has an unclosed quoted property.");
                    property = JsonSerializer.Deserialize<string>(path[literalStart..++position])!;
                }
                else
                { var start = position; while (position < path.Length && path[position] is not ('.' or '[')) position++; if (start == position) throw new ArgumentException($"JSON path '{path}' has an empty property."); property = path[start..position]; }
                values = values.Where(value => value.ValueKind == JsonValueKind.Object)
                    .SelectMany(value => value.EnumerateObject().Where(item => item.NameEquals(property)).Take(1)
                        .Select(item => item.Value)).ToList();
            }
            else if (path[position] == '[')
            {
                var end = path.IndexOf(']', position + 1);
                if (end < 0) throw new ArgumentException($"JSON path '{path}' has an unclosed array selector.");
                var selector = path[(position + 1)..end].Trim();
                values = values.Where(value => value.ValueKind == JsonValueKind.Array).SelectMany(value => SelectArray(value, selector, path)).ToList();
                position = end + 1;
            }
            else throw new ArgumentException($"JSON path '{path}' is malformed.");
        }
        return values;

        static IEnumerable<JsonElement> SelectArray(JsonElement array, string selector, string path)
        {
            if (selector == "*") return array.EnumerateArray().ToArray();
            if (selector.Equals("last", StringComparison.OrdinalIgnoreCase))
                return array.GetArrayLength() == 0 ? [] : [array[array.GetArrayLength() - 1]];
            var range = selector.Split(" to ", StringSplitOptions.TrimEntries);
            if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var finish) && start >= 0 && finish >= start)
            {
                if (start >= array.GetArrayLength()) return [];
                return Enumerable.Range(start, Math.Min(finish, array.GetArrayLength() - 1) - start + 1).Select(index => array[index]);
            }
            if (selector.Contains(','))
            {
                var length = array.GetArrayLength();
                return selector.Split(',', StringSplitOptions.TrimEntries).Select(item =>
                        item.Equals("last", StringComparison.OrdinalIgnoreCase) ? length - 1 : int.Parse(item,
                            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture))
                    .Where(index => index >= 0 && index < length).Select(index => array[index]).ToArray();
            }
            if (int.TryParse(selector, out var index) && index >= 0) return index < array.GetArrayLength() ? [array[index]] : [];
            throw new ArgumentException($"JSON path '{path}' has an invalid array selector.");
        }
    }

    private static void AddJsonDescendantEntries(JsonElement value, string path, ISet<IndexKey> entries)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) continue;
                var childPath = path + "." + JsonSerializer.Serialize(property.Name);
                entries.Add(EncodeNamedPathExists(5, childPath));
                entries.Add(EncodeNamedPathValue(4, childPath, EncodeJsonValue(property.Value)));
                AddJsonDescendantEntries(property.Value, childPath, entries);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var items = value.EnumerateArray().ToArray();
            for (var position = 0; position < items.Length; position++)
            {
                var aliases = new List<string>
                {
                    path + "[" + position.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
                    path + "[*]"
                };
                if (position == items.Length - 1) aliases.Add(path + "[last]");
                foreach (var childPath in aliases)
                {
                    entries.Add(EncodeNamedPathExists(5, childPath));
                    entries.Add(EncodeNamedPathValue(4, childPath, EncodeJsonValue(items[position])));
                    AddJsonDescendantEntries(items[position], childPath, entries);
                }
            }
        }
    }

    private static IReadOnlyList<string> ExpandJsonLookupPaths(string path)
    {
        List<string> results = [""]; var segmentStart = 0; var position = 0;
        while (position < path.Length)
        {
            if (path[position] == '"')
            {
                position++;
                while (position < path.Length && path[position] != '"')
                { if (path[position++] == '\\') position++; }
                position++;
                continue;
            }
            if (path[position] != '[') { position++; continue; }
            var end = path.IndexOf(']', position + 1);
            var prefix = path[segmentStart..position];
            var selector = path[(position + 1)..end];
            string[] selectors;
            if (selector.Contains(',')) selectors = selector.Split(',', StringSplitOptions.TrimEntries);
            else if (selector.Contains(" to ", StringComparison.Ordinal))
            {
                var bounds = selector.Split(" to ", StringSplitOptions.TrimEntries);
                var start = int.Parse(bounds[0], System.Globalization.CultureInfo.InvariantCulture);
                var finish = int.Parse(bounds[1], System.Globalization.CultureInfo.InvariantCulture);
                selectors = Enumerable.Range(start, checked(finish - start + 1))
                    .Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            }
            else selectors = [selector];
            if ((long)results.Count * selectors.Length > ushort.MaxValue)
                throw new ArgumentException("JSON path expansion exceeds the maximum SQL JSON array cardinality.", nameof(path));
            results = results.SelectMany(result => selectors.Select(selectorValue =>
                result + prefix + "[" + selectorValue + "]")).ToList();
            position = end + 1; segmentStart = position;
        }
        var suffix = path[segmentStart..];
        return results.Select(result => result + suffix).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static (int Ordinal, string Normalized, bool Exact) ResolveJsonLookupPath(CatalogIndex index, string path)
    {
        _ = CatalogSpecializedIndexOptions.Json(path); // Apply the complete declaration grammar and depth validation.
        var normalized = NormalizeJsonPath(path);
        for (var ordinal = 0; ordinal < index.SpecializedOptions!.JsonPaths.Count; ordinal++)
        {
            var promoted = NormalizeJsonPath(index.SpecializedOptions.JsonPaths[ordinal]);
            if (StringComparer.Ordinal.Equals(promoted, normalized)) return (ordinal, normalized, true);
            if (normalized.StartsWith(promoted, StringComparison.Ordinal) && normalized.Length > promoted.Length &&
                normalized[promoted.Length] is '.' or '[') return (ordinal, normalized, false);
        }
        throw new ArgumentException("JSON path is not within a subtree promoted by this index.", nameof(path));
    }

    private static string NormalizeJsonPath(string path)
    {
        var output = new StringBuilder("$"); var position = 1;
        while (position < path.Length)
        {
            if (path[position] == '.')
            {
                position++;
                if (path[position] == '*') { output.Append(".*"); position++; continue; }
                string property;
                if (path[position] == '"')
                {
                    var literalStart = position++;
                    while (path[position] != '"') { if (path[position++] == '\\') position++; }
                    property = JsonSerializer.Deserialize<string>(path[literalStart..++position])!;
                }
                else
                {
                    var start = position;
                    while (position < path.Length && path[position] is not ('.' or '[')) position++;
                    property = path[start..position];
                }
                output.Append('.').Append(JsonSerializer.Serialize(property));
            }
            else
            {
                var end = path.IndexOf(']', position + 1);
                var selector = path[(position + 1)..end].Trim();
                if (selector.Contains(',')) selector = string.Join(", ", selector.Split(',', StringSplitOptions.TrimEntries)
                    .Select(item => item.Equals("last", StringComparison.OrdinalIgnoreCase) ? "last" :
                        int.Parse(item, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                else if (selector.Contains(" to ", StringComparison.Ordinal))
                {
                    var bounds = selector.Split(" to ", StringSplitOptions.TrimEntries);
                    selector = int.Parse(bounds[0], System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " to " + int.Parse(bounds[1], System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (selector.Equals("last", StringComparison.OrdinalIgnoreCase)) selector = "last";
                else if (selector != "*") selector = int.Parse(selector, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Append('[').Append(selector).Append(']'); position = end + 1;
            }
        }
        return output.ToString();
    }

    private static byte[] EncodeJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Tagged(1, RowCodec.Utf8.GetBytes(value.GetString()!)),
        JsonValueKind.Number => Tagged(2, RowCodec.Utf8.GetBytes(NormalizeJsonNumber(value.GetRawText()))),
        JsonValueKind.True => [3, 1],
        JsonValueKind.False => [3, 0],
        JsonValueKind.Null => [4],
        JsonValueKind.Object => Tagged(5, RowCodec.Utf8.GetBytes(value.GetRawText())),
        JsonValueKind.Array => Tagged(6, RowCodec.Utf8.GetBytes(value.GetRawText())),
        _ => throw new ArgumentException("Unsupported JSON index value.")
    };

    private static byte[] EncodeJsonValue(SqlValue value) => value switch
    {
        NullSqlValue => [4],
        TextSqlValue text => Tagged(1, RowCodec.Utf8.GetBytes(text.Value)),
        BooleanSqlValue boolean => [3, boolean.Value ? (byte)1 : (byte)0],
        IntegerSqlValue integer => Tagged(2, RowCodec.Utf8.GetBytes(NormalizeJsonNumber(
            integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
        DecimalSqlValue exact => Tagged(2, RowCodec.Utf8.GetBytes(NormalizeJsonNumber(exact.Value.ToString()))),
        FloatSqlValue approximate => Tagged(2, RowCodec.Utf8.GetBytes(NormalizeJsonNumber(
            approximate.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))),
        VariantSqlValue variant => EncodeJsonValue(variant.Value),
        _ => throw new ArgumentException("JSON path lookup supports string, numeric, Boolean, and NULL scalar values.", nameof(value))
    };

    private static byte[] Tagged(byte tag, ReadOnlySpan<byte> payload)
    {
        var bytes = new byte[payload.Length + 1]; bytes[0] = tag; payload.CopyTo(bytes.AsSpan(1)); return bytes;
    }

    private static string NormalizeJsonNumber(string value)
    {
        var position = 0; var negative = false;
        if (value.Length != 0 && value[0] == '-') { negative = true; position++; }
        var exponentAt = value.IndexOfAny('e', 'E');
        var mantissaEnd = exponentAt < 0 ? value.Length : exponentAt;
        var point = value.IndexOf('.', position, mantissaEnd - position);
        var fractionalDigits = point < 0 ? 0 : mantissaEnd - point - 1;
        var digits = (point < 0 ? value[position..mantissaEnd] : value[position..point] + value[(point + 1)..mantissaEnd])
            .TrimStart('0');
        if (digits.Length == 0) return "0@0";
        var exponent = exponentAt < 0 ? 0 : int.Parse(value[(exponentAt + 1)..],
            System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture);
        var scale = checked(fractionalDigits - exponent);
        while (scale > 0 && digits.EndsWith('0')) { digits = digits[..^1]; scale--; }
        if (scale < 0) { digits += new string('0', checked(-scale)); scale = 0; }
        return (negative ? "-" : "+") + digits + "@" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IndexKey EncodePathValue(byte prefix, int ordinal, ReadOnlySpan<byte> value)
    {
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, prefix);
        Span<byte> ordinalBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(ordinalBytes, checked((ushort)ordinal)); Write(output, ordinalBytes);
        WriteEscapedBytes(output, value); return new IndexKey(output.WrittenSpan);
    }

    private static IndexKey EncodePathExists(byte prefix, int ordinal)
    {
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, prefix);
        Span<byte> ordinalBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(ordinalBytes, checked((ushort)ordinal)); Write(output, ordinalBytes);
        return new IndexKey(output.WrittenSpan);
    }

    private static IndexKey EncodeNamedPathValue(byte prefix, string path, ReadOnlySpan<byte> value)
    {
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, prefix);
        WriteEscapedBytes(output, RowCodec.Utf8.GetBytes(path)); WriteEscapedBytes(output, value);
        return new IndexKey(output.WrittenSpan);
    }

    private static IndexKey EncodeNamedPathExists(byte prefix, string path)
    {
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, prefix);
        WriteEscapedBytes(output, RowCodec.Utf8.GetBytes(path)); return new IndexKey(output.WrittenSpan);
    }

    private static IReadOnlyList<(string Path, string Value)> SelectXmlNodes(string xml, CatalogIndex index)
    {
        XDocument document; var wrapped = false;
        try { document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace); }
        catch (System.Xml.XmlException)
        {
            document = XDocument.Parse("<sqlstore-root>" + xml + "</sqlstore-root>", LoadOptions.PreserveWhitespace);
            wrapped = true;
        }
        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective)
        {
            var maximumDepth = document.Root!.DescendantsAndSelf().Select(element =>
                    element.Ancestors().Count() + (wrapped ? 0 : 1))
                .DefaultIfEmpty(0).Max();
            if (maximumDepth > 128)
                throw new ArgumentException("Selective XML indexes do not support documents deeper than 128 element levels.");
            var selected = new List<(string Path, string Value)>();
            foreach (var path in index.SpecializedOptions.JsonPaths)
            {
                var effective = wrapped && path.StartsWith('/') ? "/sqlstore-root" + (path == "/" ? "" : path) : path;
                if (path == "/") selected.Add((path, document.Root!.Value));
                else
                {
                    foreach (var node in EvaluateXmlNodes(document, effective, path, index.SpecializedOptions.XmlNamespaces))
                        selected.Add((path, XmlNodeValue(node, path)));
                }
            }
            return selected;
        }
        List<(string Path, string Value)> result = [];
        foreach (var root in wrapped ? document.Root!.Elements() : [document.Root!]) AddElement(root, "", result);
        return result;

        static void AddElement(XElement element, string parentPath, ICollection<(string Path, string Value)> output)
        {
            var path = parentPath + "/" + CanonicalXmlName(element.Name);
            output.Add((path, element.Value));
            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
                output.Add((path + "/@" + CanonicalXmlName(attribute.Name), attribute.Value));
            foreach (var child in element.Nodes())
                switch (child)
                {
                    case XElement childElement:
                        AddElement(childElement, path, output);
                        break;
                    case XCData data:
                        output.Add((path + "/text()", data.Value));
                        break;
                    case XText text:
                        output.Add((path + "/text()", text.Value));
                        break;
                    case XComment comment:
                        output.Add((path + "/comment()", comment.Value));
                        break;
                    case XProcessingInstruction instruction:
                        output.Add((path + "/processing-instruction()", instruction.Data));
                        break;
                }
        }
    }

    private static IReadOnlyList<object> EvaluateXmlNodes(XDocument document, string effectivePath, string declaredPath,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var namespaceManager = new XmlNamespaceManager(new NameTable());
        foreach (var binding in namespaces) namespaceManager.AddNamespace(binding.Key, binding.Value);
        var evaluated = document.XPathEvaluate(effectivePath, namespaceManager);
        if (evaluated is XObject node) return [node];
        if (evaluated is not IEnumerable nodes)
            throw new ArgumentException($"Selective XML path '{declaredPath}' must evaluate to nodes.");
        return nodes.Cast<object>().ToArray();
    }

    private static string XmlNodeValue(object node, string path) => node switch
    {
        XElement element => element.Value,
        XAttribute attribute => attribute.Value,
        XCData data => data.Value,
        XText text => text.Value,
        XComment comment => comment.Value,
        XProcessingInstruction instruction => instruction.Data,
        _ => throw new ArgumentException($"Selective XML path '{path}' returned an unsupported node kind.")
    };

    private static string CanonicalXmlName(XName name) => name.NamespaceName.Length == 0
        ? name.LocalName
        : "{" + name.NamespaceName + "}" + name.LocalName;

    private static string NormalizeXmlLookupPath(CatalogIndex index, string path)
    {
        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective) return path;
        if (path == "/") return path;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return "/";
        var normalized = new StringBuilder();
        foreach (var rawSegment in segments)
        {
            var attribute = rawSegment.StartsWith('@');
            var segment = attribute ? rawSegment[1..] : rawSegment;
            if (!attribute && segment is "text()" or "comment()" or "processing-instruction()")
            {
                normalized.Append('/').Append(segment);
                continue;
            }
            if (segment.Length == 0 || segment.IndexOfAny(['[', ']', '(', ')', '*']) >= 0)
                throw new ArgumentException("Primary XML index lookups require a simple absolute element or attribute path.", nameof(path));
            var colon = segment.IndexOf(':');
            string localName; string? namespaceUri = null;
            if (colon >= 0)
            {
                if (colon == 0 || colon == segment.Length - 1 || segment.IndexOf(':', colon + 1) >= 0)
                    throw new ArgumentException("XML lookup path contains an invalid qualified name.", nameof(path));
                var prefix = segment[..colon]; localName = segment[(colon + 1)..];
                if (!index.SpecializedOptions.XmlNamespaces.TryGetValue(prefix, out namespaceUri))
                    throw new ArgumentException($"XML namespace prefix '{prefix}' is not bound by this index.", nameof(path));
            }
            else localName = segment;
            try { XmlConvert.VerifyNCName(localName); }
            catch (XmlException exception) { throw new ArgumentException($"XML path name '{localName}' is invalid.", nameof(path), exception); }
            normalized.Append('/');
            if (attribute) normalized.Append('@');
            if (namespaceUri is not null) normalized.Append('{').Append(namespaceUri).Append('}');
            normalized.Append(localName);
        }
        return normalized.ToString();
    }

    private static void ValidateXmlLookupPath(CatalogIndex index, string path)
    {
        if (!path.StartsWith('/')) throw new ArgumentException("XML lookup paths must be absolute.", nameof(path));
        if (index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective &&
            !index.SpecializedOptions.JsonPaths.Contains(path, StringComparer.Ordinal))
            throw new ArgumentException("XML path is not promoted by this selective index.", nameof(path));
    }

    private static void ValidateFor(this CatalogTable table, CatalogIndex index)
    {
        if (index.TableId != table.Id) throw new ArgumentException("Index does not belong to the table.", nameof(index));
    }

    private static void WriteEscapedBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value == 0)
            {
                WriteByte(output, 0);
                WriteByte(output, byte.MaxValue);
            }
            else
                WriteByte(output, value);
        }
        WriteByte(output, 0);
        WriteByte(output, 0);
    }

    private static byte[] GetTextSortKey(string value, SqlType type, string? indexCollation)
    {
        value = value.TrimEnd(' '); // SQL Server pads the shorter operand with spaces for string comparisons.
        var collation = SqlCollation.Parse(indexCollation ?? type.Collation ?? SqlCollation.Default.Name);
        var unicode = type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText;
        return collation.GetSortKey(value, unicode);
    }

    private static void WriteSortableInt64(IBufferWriter<byte> output, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unchecked((ulong)value) ^ 0x8000000000000000UL);
        Write(output, bytes);
    }

    private static void WriteSortableDouble(IBufferWriter<byte> output, double value)
    {
        var bits = BitConverter.DoubleToUInt64Bits(value);
        bits = (bits & 0x8000000000000000UL) != 0 ? ~bits : bits ^ 0x8000000000000000UL;
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, bits);
        Write(output, bytes);
    }

    private static void WriteDecimal(IBufferWriter<byte> output, SqlDecimal value)
    {
        value.TryRescale(38, out var coefficient);
        var magnitude = BigInteger.Abs(coefficient);
        Span<byte> bytes = stackalloc byte[33];
        bytes.Clear();
        var negative = coefficient.Sign < 0;
        bytes[0] = negative ? (byte)0x7F : (byte)0x80;
        var magnitudeLength = magnitude.GetByteCount(isUnsigned: true);
        if (!magnitude.TryWriteBytes(bytes[(bytes.Length - magnitudeLength)..], out _, isUnsigned: true, isBigEndian: true))
            throw new InvalidOperationException("SQL decimal did not fit its canonical index representation.");
        if (negative)
            for (var index = 1; index < bytes.Length; index++) bytes[index] ^= 0xFF;
        Write(output, bytes);
    }

    private static void WriteVariant(IBufferWriter<byte> output, SqlValue value, SqlType declaredType)
    {
        switch (GetVariantFamilyOrder(declaredType.Name))
        {
            case 1:
                WriteSqlGuid(output, ((UniqueIdentifierSqlValue)value).Value);
                break;
            case 2:
                WriteEscapedBytes(output, ((BinarySqlValue)value).Value.Span);
                break;
            case 3:
                var collation = declaredType.CollationMetadata ?? SqlCollation.Default;
                Span<byte> metadata = stackalloc byte[6];
                BinaryPrimitives.WriteUInt16BigEndian(metadata, collation.Lcid);
                metadata[2] = collation.Version;
                BinaryPrimitives.WriteUInt16BigEndian(metadata[3..], (ushort)collation.ComparisonFlags);
                metadata[5] = collation.SortId;
                Write(output, metadata);
                WriteEscapedBytes(output, GetTextSortKey(((TextSqlValue)value).Value, declaredType, null));
                break;
            case 4:
                WriteDecimal(output, value switch
                {
                    BooleanSqlValue boolean => new SqlDecimal(boolean.Value ? 1 : 0, 0),
                    IntegerSqlValue integer => new SqlDecimal(integer.Value, 0),
                    DecimalSqlValue exact => exact.Value,
                    _ => throw new ArgumentException("Invalid exact-numeric sql_variant value.", nameof(value))
                });
                break;
            case 5:
                WriteSortableDouble(output, ((FloatSqlValue)value).Value);
                break;
            case 6:
                WriteSortableInt64(output, value switch
                {
                    DateSqlValue date => date.Value.ToDateTime(TimeOnly.MinValue).Ticks,
                    TimeSqlValue time => new DateTime(1900, 1, 1).Ticks + time.Value.Ticks,
                    DateTimeSqlValue dateTime => dateTime.Value.Ticks,
                    DateTimeOffsetSqlValue offset => offset.Value.UtcTicks,
                    _ => throw new ArgumentException("Invalid temporal sql_variant value.", nameof(value))
                });
                break;
            default:
                throw new ArgumentException("Unsupported sql_variant index value.", nameof(value));
        }
    }

    private static byte GetVariantFamilyOrder(SqlTypeName type) => type switch
    {
        SqlTypeName.UniqueIdentifier => 1,
        SqlTypeName.VarBinary or SqlTypeName.Binary => 2,
        SqlTypeName.NVarChar or SqlTypeName.NChar or SqlTypeName.VarChar or SqlTypeName.Char => 3,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney or
            SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or SqlTypeName.Bit => 4,
        SqlTypeName.Float or SqlTypeName.Real => 5,
        SqlTypeName.DateTimeOffset or SqlTypeName.DateTime2 or SqlTypeName.DateTime or SqlTypeName.SmallDateTime or
            SqlTypeName.Date or SqlTypeName.Time => 6,
        _ => throw new ArgumentException($"{type} cannot be stored in sql_variant.", nameof(type))
    };
    private static void WriteSqlGuid(IBufferWriter<byte> output, Guid value)
    {
        Span<byte> source = stackalloc byte[16];
        value.TryWriteBytes(source);
        ReadOnlySpan<byte> order = [10, 11, 12, 13, 14, 15, 8, 9, 7, 6, 5, 4, 3, 2, 1, 0];
        Span<byte> ordered = stackalloc byte[16];
        for (var index = 0; index < ordered.Length; index++) ordered[index] = source[order[index]];
        Write(output, ordered);
    }
    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value) { value.CopyTo(output.GetSpan(value.Length)); output.Advance(value.Length); }
}

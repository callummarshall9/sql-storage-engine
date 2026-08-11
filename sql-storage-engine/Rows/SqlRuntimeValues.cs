using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

/// <summary>A canonical hierarchyid path with the core SQL hierarchy navigation operations.</summary>
public sealed class SqlHierarchyId : IComparable<SqlHierarchyId>, IEquatable<SqlHierarchyId>
{
    private const byte BinaryVersion = 2;
    private readonly BigInteger[][] _labels;

    private SqlHierarchyId(IEnumerable<IEnumerable<BigInteger>> labels) =>
        _labels = labels.Select(label => label.ToArray()).ToArray();

    public static SqlHierarchyId Root { get; } = new([]);
    public int GetLevel() => _labels.Length;

    public static SqlHierarchyId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value == "/") return Root;
        if (value.Length < 3 || value[0] != '/' || value[^1] != '/')
            throw new FormatException("A hierarchyid path must begin and end with '/'.");
        var parts = value[1..^1].Split('/');
        var labels = new BigInteger[parts.Length][];
        for (var level = 0; level < parts.Length; level++)
        {
            var components = parts[level].Split('.');
            if (components.Length == 0) throw new FormatException("A hierarchyid label cannot be empty.");
            labels[level] = new BigInteger[components.Length];
            for (var component = 0; component < components.Length; component++)
            {
                var token = components[component];
                if (!BigInteger.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                        out labels[level][component]) ||
                    !StringComparer.Ordinal.Equals(token, labels[level][component].ToString(CultureInfo.InvariantCulture)))
                    throw new FormatException($"Invalid hierarchyid integer '{token}'.");
            }
        }
        return new SqlHierarchyId(labels);
    }

    public SqlHierarchyId? GetAncestor(int levels)
    {
        if (levels < 0) throw new ArgumentOutOfRangeException(nameof(levels));
        if (levels > _labels.Length) return null;
        return new SqlHierarchyId(_labels.Take(_labels.Length - levels));
    }

    public bool IsDescendantOf(SqlHierarchyId parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent._labels.Length > _labels.Length) return false;
        for (var level = 0; level < parent._labels.Length; level++)
            if (!parent._labels[level].AsSpan().SequenceEqual(_labels[level])) return false;
        return true;
    }

    public SqlHierarchyId GetReparentedValue(SqlHierarchyId oldRoot, SqlHierarchyId newRoot)
    {
        ArgumentNullException.ThrowIfNull(oldRoot); ArgumentNullException.ThrowIfNull(newRoot);
        if (!IsDescendantOf(oldRoot)) throw new ArgumentException("The value is not below oldRoot.", nameof(oldRoot));
        return new SqlHierarchyId(newRoot._labels.Concat(_labels.Skip(oldRoot._labels.Length)));
    }

    public SqlHierarchyId GetDescendant(SqlHierarchyId? left, SqlHierarchyId? right)
    {
        if (left is not null && (left.GetLevel() != GetLevel() + 1 || !left.IsDescendantOf(this)))
            throw new ArgumentException("left must be an immediate child.", nameof(left));
        if (right is not null && (right.GetLevel() != GetLevel() + 1 || !right.IsDescendantOf(this)))
            throw new ArgumentException("right must be an immediate child.", nameof(right));
        var leftLabel = left?._labels[^1]; var rightLabel = right?._labels[^1];
        if (leftLabel is not null && rightLabel is not null && CompareLabel(leftLabel, rightLabel) >= 0)
            throw new ArgumentException("left must sort before right.");

        BigInteger[] label;
        if (leftLabel is null && rightLabel is null) label = [BigInteger.One];
        else if (leftLabel is null) label = [rightLabel![0] - BigInteger.One];
        else if (rightLabel is null) label = [leftLabel[0] + BigInteger.One];
        else if (IsPrefix(leftLabel, rightLabel))
        {
            label = leftLabel.Append(rightLabel[leftLabel.Length] - BigInteger.One).ToArray();
        }
        else
        {
            // Extending the lower dictionary key always leaves room before a non-prefix upper key.
            label = leftLabel.Append(BigInteger.One).ToArray();
        }
        return new SqlHierarchyId(_labels.Append(label));
    }

    public int CompareTo(SqlHierarchyId? other)
    {
        if (other is null) return 1;
        for (var index = 0; index < Math.Min(_labels.Length, other._labels.Length); index++)
        { var result = CompareLabel(_labels[index], other._labels[index]); if (result != 0) return result; }
        return _labels.Length.CompareTo(other._labels.Length);
    }

    public byte[] Serialize()
    {
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output, BinaryVersion); WriteVarUInt(output, checked((uint)_labels.Length));
        foreach (var label in _labels)
        {
            WriteVarUInt(output, checked((uint)label.Length));
            foreach (var component in label) WriteInteger(output, component);
        }
        if (output.WrittenCount > 892)
            throw new InvalidOperationException("The hierarchyid representation exceeds 892 bytes.");
        return output.WrittenSpan.ToArray();
    }

    public static SqlHierarchyId Deserialize(ReadOnlySpan<byte> value)
    {
        if (value.Length > 892) throw new StorageFormatException("The hierarchyid representation exceeds 892 bytes.");
        if (value.IsEmpty || value[0] != BinaryVersion)
            throw new StorageFormatException("Unsupported hierarchyid binary version.");
        var reader = new HierarchyReader(value[1..]);
        var levelCount = reader.VarUInt();
        var labels = new BigInteger[checked((int)levelCount)][];
        for (var level = 0; level < labels.Length; level++)
        {
            var componentCount = reader.VarUInt();
            if (componentCount == 0) throw new StorageFormatException("A hierarchyid label cannot be empty.");
            labels[level] = new BigInteger[checked((int)componentCount)];
            for (var component = 0; component < labels[level].Length; component++)
                labels[level][component] = reader.Integer();
        }
        if (!reader.End) throw new StorageFormatException("Hierarchyid contains trailing bytes.");
        return new SqlHierarchyId(labels);
    }

    /// <summary>Returns a bytewise-sortable depth-first key for B-tree indexes.</summary>
    internal byte[] GetSortKey()
    {
        var output = new ArrayBufferWriter<byte>();
        foreach (var label in _labels)
        {
            foreach (var component in label) WriteSortableInteger(output, component);
            WriteByte(output, 0); // A dictionary prefix sorts before every extension.
        }
        return output.WrittenSpan.ToArray();
    }

    public override string ToString() => _labels.Length == 0 ? "/" : "/" + string.Join('/', _labels.Select(
        label => string.Join('.', label.Select(component => component.ToString(CultureInfo.InvariantCulture))))) + "/";

    public bool Equals(SqlHierarchyId? other)
    {
        if (other is null || _labels.Length != other._labels.Length) return false;
        for (var level = 0; level < _labels.Length; level++)
            if (!_labels[level].AsSpan().SequenceEqual(other._labels[level])) return false;
        return true;
    }
    public override bool Equals(object? obj) => Equals(obj as SqlHierarchyId);
    public override int GetHashCode()
    { var hash = new HashCode(); foreach (var label in _labels) foreach (var component in label) hash.Add(component); return hash.ToHashCode(); }

    private static int CompareLabel(ReadOnlySpan<BigInteger> left, ReadOnlySpan<BigInteger> right)
    {
        for (var component = 0; component < Math.Min(left.Length, right.Length); component++)
        { var result = left[component].CompareTo(right[component]); if (result != 0) return result; }
        return left.Length.CompareTo(right.Length);
    }

    private static bool IsPrefix(ReadOnlySpan<BigInteger> prefix, ReadOnlySpan<BigInteger> value) =>
        prefix.Length < value.Length && prefix.SequenceEqual(value[..prefix.Length]);

    private static void WriteInteger(IBufferWriter<byte> output, BigInteger value)
    {
        WriteByte(output, value.Sign switch { < 0 => (byte)0, 0 => (byte)1, _ => (byte)2 });
        var magnitude = BigInteger.Abs(value).ToByteArray(isUnsigned: true, isBigEndian: true);
        WriteVarUInt(output, checked((uint)magnitude.Length)); Write(output, magnitude);
    }

    private static void WriteSortableInteger(IBufferWriter<byte> output, BigInteger value)
    {
        var magnitude = BigInteger.Abs(value).ToByteArray(isUnsigned: true, isBigEndian: true);
        if (magnitude.Length > ushort.MaxValue) throw new InvalidOperationException("Hierarchyid integer is too large.");
        Span<byte> length = stackalloc byte[2];
        if (value.Sign < 0)
        {
            WriteByte(output, 0x10);
            BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)(ushort.MaxValue - magnitude.Length)));
            Write(output, length);
            foreach (var item in magnitude) WriteByte(output, (byte)~item);
        }
        else
        {
            WriteByte(output, 0x20);
            BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)magnitude.Length)); Write(output, length);
            Write(output, magnitude);
        }
    }

    private static void WriteVarUInt(IBufferWriter<byte> output, uint value)
    {
        while (value >= 0x80) { WriteByte(output, (byte)(value | 0x80)); value >>= 7; }
        WriteByte(output, (byte)value);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value)
    { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    { value.CopyTo(output.GetSpan(value.Length)); output.Advance(value.Length); }

    private ref struct HierarchyReader
    {
        private readonly ReadOnlySpan<byte> _source; private int _offset;
        public HierarchyReader(ReadOnlySpan<byte> source) { _source = source; _offset = 0; }
        public bool End => _offset == _source.Length;
        public uint VarUInt()
        {
            uint value = 0; var shift = 0;
            for (var count = 0; count < 5; count++)
            {
                var next = Byte(); value |= (uint)(next & 0x7f) << shift;
                if ((next & 0x80) == 0) return value;
                shift += 7;
            }
            throw new StorageFormatException("Hierarchyid contains an invalid variable integer.");
        }
        public BigInteger Integer()
        {
            var sign = Byte(); if (sign > 2) throw new StorageFormatException("Hierarchyid contains an invalid integer sign.");
            var length = VarUInt();
            if (length > 892 || _offset > _source.Length - length) throw new StorageFormatException("Hierarchyid is truncated.");
            var magnitude = new BigInteger(_source.Slice(_offset, checked((int)length)), isUnsigned: true, isBigEndian: true);
            _offset += checked((int)length);
            if ((sign == 1) != magnitude.IsZero || sign != 1 && magnitude.IsZero)
                throw new StorageFormatException("Hierarchyid integer is not canonically encoded.");
            return sign == 0 ? -magnitude : magnitude;
        }
        private byte Byte()
        { if (_offset >= _source.Length) throw new StorageFormatException("Hierarchyid is truncated."); return _source[_offset++]; }
    }
}

public enum SqlSpatialKind : byte { Geometry = 1, Geography = 2 }

/// <summary>A parsed spatial value carrying SQL Server's kind, SRID, and structured geometry.</summary>
public sealed record SqlSpatial
{
    private readonly SqlSpatialShape _shape;

    public SqlSpatial(SqlSpatialKind kind, int srid, string wellKnownText)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(wellKnownText);
        _shape = SqlSpatialParser.Parse(wellKnownText);
        if (_shape.Type == SqlSpatialShapeType.FullGlobe && kind != SqlSpatialKind.Geography)
            throw new FormatException("FULLGLOBE is valid only for geography.");
        Kind = kind; Srid = srid;
        if (kind == SqlSpatialKind.Geography) SqlSpatialOperations.ValidateGeography(_shape);
    }

    private SqlSpatial(SqlSpatialKind kind, int srid, SqlSpatialShape shape)
    { Kind = kind; Srid = srid; _shape = shape; }

    public SqlSpatialKind Kind { get; }
    public int Srid { get; }
    public string WellKnownText => _shape.ToWkt();
    public string STAsText() => WellKnownText;
    public byte[] STAsBinary() => SqlSpatialWkbCodec.Encode(_shape);
    public string AsGml() => SqlSpatialGml.Write(_shape, Srid);
    public string STGeometryType() => _shape.Type.ToSqlName();
    public int STDimension() => _shape.Dimension;
    public bool STIsEmpty() => _shape.IsEmpty;
    public bool STIsValid() => SqlSpatialOperations.IsValid(_shape);
    public int STNumPoints() => _shape.Coordinates().Count();
    public double STLength() => SqlSpatialOperations.Length(_shape, Kind);
    public double STArea() => SqlSpatialOperations.Area(_shape, Kind);
    public bool? STEquals(SqlSpatial other) => Compatible(other) ? SqlSpatialOperations.EqualsTopologically(_shape, other._shape) : null;
    public bool? STIntersects(SqlSpatial other) => Compatible(other) ? SqlSpatialOperations.Intersects(_shape, other._shape) : null;
    public bool? STDisjoint(SqlSpatial other) => STIntersects(other) is { } intersects ? !intersects : null;
    public bool? STContains(SqlSpatial other) => Compatible(other) ? SqlSpatialOperations.Contains(_shape, other._shape) : null;
    public bool? STWithin(SqlSpatial other) { ArgumentNullException.ThrowIfNull(other); return other.STContains(this); }

    public double? STDistance(SqlSpatial other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Compatible(other) ? SqlSpatialOperations.Distance(_shape, other._shape, Kind) : null;
    }

    public SqlSpatial STEnvelope() => new(Kind, Srid, SqlSpatialOperations.Envelope(_shape));
    public SqlSpatial? STStartPoint() => PointAt(1);
    public SqlSpatial? STEndPoint() => PointAt(STNumPoints());
    public SqlSpatial? STPointN(int position) => PointAt(position);
    public byte[] Serialize() => SqlSpatialBinaryCodec.Encode(Kind, Srid, _shape);

    public static SqlSpatial FromWellKnownBinary(SqlSpatialKind kind, ReadOnlySpan<byte> value, int srid = 0) =>
        new(kind, srid, SqlSpatialWkbCodec.Decode(value));

    public static SqlSpatial Deserialize(ReadOnlySpan<byte> value, SqlSpatialKind expectedKind)
    {
        var decoded = SqlSpatialBinaryCodec.Decode(value);
        if (decoded.Kind != expectedKind) throw new StorageFormatException("Spatial representation has the wrong kind.");
        if (expectedKind == SqlSpatialKind.Geography) SqlSpatialOperations.ValidateGeography(decoded.Shape);
        return new SqlSpatial(decoded.Kind, decoded.Srid, decoded.Shape);
    }

    internal SqlSpatialEnvelope Bounds => SqlSpatialOperations.Bounds(_shape);

    public bool Equals(SqlSpatial? other) => other is not null && Kind == other.Kind && Srid == other.Srid &&
        StringComparer.Ordinal.Equals(WellKnownText, other.WellKnownText);
    public override int GetHashCode() => HashCode.Combine(Kind, Srid, StringComparer.Ordinal.GetHashCode(WellKnownText));

    private bool Compatible(SqlSpatial other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Kind == other.Kind && Srid == other.Srid;
    }

    private SqlSpatial? PointAt(int position)
    {
        if (position < 1) throw new ArgumentOutOfRangeException(nameof(position));
        var coordinates = _shape.Coordinates().ToArray();
        return position > coordinates.Length ? null : new SqlSpatial(Kind, Srid, SqlSpatialShape.Point(coordinates[position - 1]));
    }
}

public sealed record HierarchyIdSqlValue(SqlHierarchyId Value) : SqlValue
{ public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Binary; }
public sealed record SpatialSqlValue(SqlSpatial Value) : SqlValue
{ public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Binary; }

/// <summary>An execution-scoped cursor value; it is intentionally never row-serializable.</summary>
public sealed record CursorSqlValue(IReadOnlyList<Row> Rows, int Position = -1, bool IsOpen = true) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => null;
    public CursorSqlValue FetchNext(out Row? row)
    {
        EnsureOpen(); var next = Position + 1; row = next < Rows.Count ? Rows[next] : null;
        return this with { Position = next };
    }
    public CursorSqlValue FetchPrior(out Row? row) => FetchAt(Position - 1, out row);
    public CursorSqlValue FetchFirst(out Row? row) => FetchAt(0, out row);
    public CursorSqlValue FetchLast(out Row? row) => FetchAt(Rows.Count - 1, out row);
    public CursorSqlValue FetchAbsolute(int position, out Row? row) => FetchAt(position > 0 ? position - 1 : Rows.Count + position, out row);
    public CursorSqlValue FetchRelative(int offset, out Row? row) => FetchAt(Position + offset, out row);
    public CursorSqlValue Close() => this with { IsOpen = false, Position = -1 };
    private CursorSqlValue FetchAt(int position, out Row? row)
    { EnsureOpen(); row = position >= 0 && position < Rows.Count ? Rows[position] : null; return this with { Position = position }; }
    private void EnsureOpen() { if (!IsOpen) throw new InvalidOperationException("The cursor is closed."); }
}

/// <summary>An execution-scoped table value validated against anonymous or cataloged table metadata.</summary>
public sealed record TableSqlValue : SqlValue
{
    public TableSqlValue(TableDefinition definition, IEnumerable<Row> rows)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ArgumentNullException.ThrowIfNull(rows); var snapshot = rows.ToArray();
        foreach (var row in snapshot) definition.ValidateRow(row); Rows = Array.AsReadOnly(snapshot);
    }
    public TableSqlValue(CatalogTableType definition, IEnumerable<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(definition); ArgumentNullException.ThrowIfNull(rows);
        Definition = new TableDefinition(definition.Columns.Select(column => new ColumnDefinition(column.Id, column.Name,
            column.Type, column.IsNullable, column.Encryption)));
        var keyTable = new CatalogTable(new TableId(1), "table_value", 1, new PageId(1), definition.Columns);
        var tableIndexes = definition.Indexes.Select((index, position) => new TableValueIndex(
            index,
            new CatalogIndex(new IndexId(checked((ulong)position + 1)), index.Name, keyTable.Id, new PageId(1), true,
                index.Columns, storageKind: CatalogIndexStorageKind.NonClustered),
            [])).ToArray();
        BigInteger? identity = definition.Columns.SingleOrDefault(column => column.Identity is not null)?.Identity?.Seed;
        ulong rowVersion = 1;
        Span<byte> rowVersionBytes = stackalloc byte[8];
        var normalized = new List<Row>();
        foreach (var row in rows)
        {
            if (row.Values.Count != definition.Columns.Count) throw new ArgumentException("Row width does not match table type.", nameof(rows));
            var values = row.Values.ToArray(); var context = new Dictionary<string, SqlValue>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < definition.Columns.Count; index++)
            {
                var column = definition.Columns[index]; var value = values[index];
                if (column.Identity is not null)
                {
                    if (value is not DefaultSqlValue && !value.IsNull)
                        throw new ArgumentException("Table-type IDENTITY cannot be assigned.", nameof(rows));
                    value = SqlConversion.ConvertTo(column.Type, SqlValue.Decimal(new SqlDecimal(identity!.Value, 0)));
                    identity += column.Identity.Increment;
                }
                else if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
                {
                    if (value is not DefaultSqlValue && !value.IsNull)
                        throw new ArgumentException("Table-type rowversion cannot be assigned explicitly.", nameof(rows));
                    BinaryPrimitives.WriteUInt64BigEndian(rowVersionBytes, rowVersion++);
                    value = SqlValue.Binary(rowVersionBytes);
                }
                else if (column.IsComputed) value = SqlValue.Default;
                else if (value is DefaultSqlValue) value = column.DefaultExpression is null ? SqlValue.Null : SqlExpressions.Evaluate(column.DefaultExpression, context);
                if (value is not DefaultSqlValue && !value.IsNull) value = SqlConversion.ConvertTo(column.Type, value);
                values[index] = value; if (value is not DefaultSqlValue) context[column.Name] = value;
            }
            foreach (var index in CatalogTable.GetComputedColumnOrder(definition.Columns))
            {
                var column = definition.Columns[index];
                var value = SqlExpressions.Evaluate(column.ComputedExpression!, context);
                var source = CatalogTable.ResolveDirectColumnReference(column.ComputedExpression!, definition.Columns);
                values[index] = value.IsNull ? value : source is null
                    ? SqlConversion.ConvertTo(column.Type, value)
                    : SqlConversion.ConvertTo(column.Type, source.Type, value);
                context[column.Name] = values[index];
            }
            var result = new Row(values); Definition.ValidateRow(result);
            foreach (var check in definition.CheckConstraints)
            { var checkedValue = SqlExpressions.Evaluate(check.Expression, context); if (!checkedValue.IsNull && !((BooleanSqlValue)SqlConversion.ConvertTo(SqlType.Bit, checkedValue)).Value) throw new ArgumentException($"CHECK constraint '{check.Name}' rejected a table value row.", nameof(rows)); }
            var candidateKeys = tableIndexes.Select(index =>
            {
                var keyValues = index.Definition.Columns.Select(indexed => result.Values[definition.Columns
                    .Select((column, offset) => (column, offset)).Single(item => item.column.Id == indexed.ColumnId).offset])
                    .ToArray();
                var maximum = definition.IsMemoryOptimized
                    ? index.Definition.StorageKind == CatalogIndexStorageKind.Hash ? int.MaxValue : 2_500
                    : index.Definition.StorageKind == CatalogIndexStorageKind.Clustered
                        ? CatalogIndexKey.MaximumClusteredKeyBytes : CatalogIndexKey.MaximumNonClusteredKeyBytes;
                return (Index: index, Key: CatalogIndexKey.EncodeValues(keyValues, keyTable, index.CatalogIndex, maximum));
            }).ToArray();
            var hardDuplicate = candidateKeys.FirstOrDefault(candidate =>
                candidate.Index.Definition.IsUnique && candidate.Index.Keys.Contains(candidate.Key) &&
                !candidate.Index.Definition.IgnoreDuplicateKey);
            if (hardDuplicate.Index is not null)
                throw new ArgumentException($"Unique table-type index '{hardDuplicate.Index.Definition.Name}' rejected a duplicate row.", nameof(rows));
            if (candidateKeys.Any(candidate => candidate.Index.Definition.IsUnique &&
                                               candidate.Index.Keys.Contains(candidate.Key))) continue;
            foreach (var candidate in candidateKeys.Where(candidate => candidate.Index.Definition.IsUnique))
                candidate.Index.Keys.Add(candidate.Key);
            normalized.Add(result);
        }
        Rows = normalized.AsReadOnly();
    }
    public TableDefinition Definition { get; }
    public IReadOnlyList<Row> Rows { get; }
    public override SqlStorageFamily? StorageFamily => null;

    private sealed record TableValueIndex(CatalogTableTypeIndex Definition, CatalogIndex CatalogIndex,
        HashSet<IndexKey> Keys);
}

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Catalog;

/// <summary>Encodes the self-describing version-one bootstrap catalog without relying on user schemas.</summary>
public static class CatalogCodec
{
    public const ushort FormatVersion = 1;
    public const uint Magic = 0x31544143; // "CAT1" in persisted little-endian byte order.
    public const int HeaderLength = 16;
    public const int MaximumRecordCount = 65_535;
    public const int MaximumStringBytes = 65_535;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Encode(CatalogDefinition catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateCount(catalog.Tables.Count, nameof(catalog));
        ValidateCount(catalog.Indexes.Count, nameof(catalog));
        var output = new ArrayBufferWriter<byte>();
        WriteUInt32(output, Magic);
        WriteUInt16(output, FormatVersion);
        WriteUInt16(output, 0);
        WriteUInt32(output, checked((uint)catalog.Tables.Count));
        WriteUInt32(output, checked((uint)catalog.Indexes.Count));
        foreach (var table in catalog.Tables)
        {
            WriteUInt64(output, table.Id.Value);
            WriteString(output, table.Name);
            WriteUInt64(output, table.SchemaVersion);
            WriteUInt64(output, table.FirstHeapPageId.Value);
            WriteUInt32(output, checked((uint)table.Columns.Count));
            foreach (var column in table.Columns)
            {
                WriteUInt64(output, column.Id.Value);
                WriteString(output, column.Name);
                WriteByte(output, (byte)column.Type);
                WriteByte(output, column.IsNullable ? (byte)1 : (byte)0);
                WriteUInt16(output, 0);
            }
        }
        foreach (var index in catalog.Indexes)
        {
            WriteUInt64(output, index.Id.Value);
            WriteString(output, index.Name);
            WriteUInt64(output, index.TableId.Value);
            WriteUInt64(output, index.RootPageId.Value);
            WriteByte(output, index.IsUnique ? (byte)1 : (byte)0);
            WriteByte(output, 0);
            WriteUInt16(output, checked((ushort)index.Columns.Count));
            foreach (var column in index.Columns)
            {
                WriteUInt64(output, column.ColumnId.Value);
                WriteByte(output, (byte)column.Direction);
                WriteByte(output, (byte)column.NullSortOrder);
                WriteString(output, column.Collation ?? string.Empty);
            }
        }
        return output.WrittenSpan.ToArray();
    }

    public static CatalogDefinition Decode(ReadOnlySpan<byte> source)
    {
        var reader = new Reader(source);
        if (reader.UInt32() != Magic) throw new StorageFormatException("Invalid bootstrap catalog magic number.");
        var version = reader.UInt16();
        if (version != FormatVersion) throw new StorageFormatException($"Unsupported catalog format version {version}.");
        if (reader.UInt16() != 0) throw new StorageFormatException("Reserved catalog header bytes must be zero.");
        var tableCount = reader.Count();
        var indexCount = reader.Count();
        List<CatalogTable> tables = new(tableCount);
        for (var tableNumber = 0; tableNumber < tableCount; tableNumber++)
        {
            var id = new TableId(reader.UInt64());
            var name = reader.String();
            var schemaVersion = reader.UInt64();
            var heapRoot = new PageId(reader.UInt64());
            var columnCount = reader.Count(nonZero: true);
            List<CatalogColumn> columns = new(columnCount);
            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                var columnId = new ColumnId(reader.UInt64());
                var columnName = reader.String();
                var type = (SqlType)reader.Byte();
                var nullable = reader.Boolean();
                if (reader.UInt16() != 0) throw new StorageFormatException("Reserved column bytes must be zero.");
                try { columns.Add(new CatalogColumn(columnId, columnName, type, nullable)); }
                catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog column record.", exception); }
            }
            try { tables.Add(new CatalogTable(id, name, schemaVersion, heapRoot, columns)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog table record.", exception); }
        }
        List<CatalogIndex> indexes = new(indexCount);
        for (var indexNumber = 0; indexNumber < indexCount; indexNumber++)
        {
            var id = new IndexId(reader.UInt64());
            var name = reader.String();
            var tableId = new TableId(reader.UInt64());
            var root = new PageId(reader.UInt64());
            var unique = reader.Boolean();
            if (reader.Byte() != 0) throw new StorageFormatException("Reserved index bytes must be zero.");
            var columnCount = reader.UInt16();
            if (columnCount == 0) throw new StorageFormatException("Catalog index has no columns.");
            List<CatalogIndexedColumn> columns = new(columnCount);
            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                var columnId = new ColumnId(reader.UInt64());
                var direction = (SortDirection)reader.Byte();
                var nullOrder = (NullSortOrder)reader.Byte();
                var collation = reader.String();
                try { columns.Add(new CatalogIndexedColumn(columnId, direction, nullOrder,
                    collation.Length == 0 ? null : collation)); }
                catch (ArgumentException exception) { throw new StorageFormatException("Invalid indexed-column record.", exception); }
            }
            try { indexes.Add(new CatalogIndex(id, name, tableId, root, unique, columns)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog index record.", exception); }
        }
        if (!reader.End) throw new StorageFormatException("Bootstrap catalog contains trailing bytes.");
        try { return new CatalogDefinition(tables, indexes); }
        catch (ArgumentException exception) { throw new StorageCorruptionException("Bootstrap catalog cross-references are invalid.", exception); }
    }

    private static void ValidateCount(int count, string parameterName)
    {
        if (count > MaximumRecordCount) throw new ArgumentException("Catalog record count exceeds the format limit.", parameterName);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void WriteUInt16(IBufferWriter<byte> output, ushort value) { var span = output.GetSpan(2); BinaryPrimitives.WriteUInt16LittleEndian(span, value); output.Advance(2); }
    private static void WriteUInt32(IBufferWriter<byte> output, uint value) { var span = output.GetSpan(4); BinaryPrimitives.WriteUInt32LittleEndian(span, value); output.Advance(4); }
    private static void WriteUInt64(IBufferWriter<byte> output, ulong value) { var span = output.GetSpan(8); BinaryPrimitives.WriteUInt64LittleEndian(span, value); output.Advance(8); }
    private static void WriteString(IBufferWriter<byte> output, string value)
    {
        var length = Utf8.GetByteCount(value);
        if (length > MaximumStringBytes) throw new ArgumentException("Catalog string exceeds the format limit.", nameof(value));
        WriteUInt16(output, checked((ushort)length));
        var span = output.GetSpan(length);
        Utf8.GetBytes(value, span);
        output.Advance(length);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _source;
        private int _offset;
        public Reader(ReadOnlySpan<byte> source) => _source = source;
        public bool End => _offset == _source.Length;
        public byte Byte() { Require(1); return _source[_offset++]; }
        public bool Boolean() => Byte() switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid catalog Boolean value.") };
        public ushort UInt16() { Require(2); var value = BinaryPrimitives.ReadUInt16LittleEndian(_source[_offset..]); _offset += 2; return value; }
        public uint UInt32() { Require(4); var value = BinaryPrimitives.ReadUInt32LittleEndian(_source[_offset..]); _offset += 4; return value; }
        public ulong UInt64() { Require(8); var value = BinaryPrimitives.ReadUInt64LittleEndian(_source[_offset..]); _offset += 8; return value; }
        public int Count(bool nonZero = false) { var value = UInt32(); if (value > MaximumRecordCount || nonZero && value == 0) throw new StorageFormatException("Invalid catalog record count."); return checked((int)value); }
        public string String()
        {
            var length = UInt16(); Require(length);
            try { var value = Utf8.GetString(_source.Slice(_offset, length)); _offset += length; return value; }
            catch (DecoderFallbackException exception) { throw new StorageFormatException("Catalog string is not valid UTF-8.", exception); }
        }
        private void Require(int length) { if (length < 0 || _offset > _source.Length - length) throw new StorageFormatException("Bootstrap catalog record is truncated."); }
    }
}

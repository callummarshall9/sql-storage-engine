using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Catalog;

/// <summary>Builds deterministic composite keys from catalog ordering metadata.</summary>
public static class CatalogIndexKey
{
    public static IndexKey Encode(Row row, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        if (index.TableId != table.Id) throw new ArgumentException("Index does not belong to the table.", nameof(index));
        var output = new ArrayBufferWriter<byte>();
        foreach (var indexed in index.Columns)
        {
            var segment = new ArrayBufferWriter<byte>();
            var position = table.Columns.Select((column, offset) => (column, offset))
                .Single(item => item.column.Id == indexed.ColumnId).offset;
            var value = row.Values[position];
            if (value.IsNull)
                WriteByte(segment, indexed.NullSortOrder == NullSortOrder.First ? (byte)0 : (byte)255);
            else
            {
                WriteByte(segment, indexed.NullSortOrder == NullSortOrder.First ? (byte)1 : (byte)254);
                switch (value)
                {
                    case BooleanSqlValue boolean: WriteByte(segment, boolean.Value ? (byte)1 : (byte)0); break;
                    case IntegerSqlValue integer:
                        var integerBytes = new byte[8];
                        BinaryPrimitives.WriteUInt64BigEndian(integerBytes, unchecked((ulong)integer.Value) ^ 0x8000000000000000UL);
                        Write(segment, integerBytes);
                        break;
                    case TextSqlValue text: WriteLengthBytes(segment, RowCodec.Utf8.GetBytes(text.Value)); break;
                    case BinarySqlValue binary: WriteLengthBytes(segment, binary.Value.Span); break;
                    default: throw new ArgumentException("Unsupported indexed SQL value.", nameof(row));
                }
            }
            var encodedSegment = segment.WrittenSpan.ToArray();
            if (indexed.Direction == SortDirection.Descending)
                for (var offset = 0; offset < encodedSegment.Length; offset++) encodedSegment[offset] ^= 0xFF;
            Write(output, encodedSegment);
        }
        return new IndexKey(output.WrittenSpan);
    }

    private static void WriteLengthBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        Write(output, length);
        Write(output, bytes);
    }
    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value) { value.CopyTo(output.GetSpan(value.Length)); output.Advance(value.Length); }
}

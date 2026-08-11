using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
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
        table.ValidateFor(index);
        if (row.Values.Count != table.Columns.Count)
            throw new ArgumentException("Row width does not match the table definition.", nameof(row));
        return EncodeValues(index.Columns.Select(indexed =>
        {
            var position = table.Columns.Select((column, offset) => (column, offset))
                .Single(item => item.column.Id == indexed.ColumnId).offset;
            return row.Values[position];
        }).ToArray(), table, index);
    }

    /// <summary>Encodes values in index-column order for exact lookup and bounded index scans.</summary>
    public static IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable table, CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(index);
        table.ValidateFor(index);
        if (values.Count != index.Columns.Count)
            throw new ArgumentException($"Expected {index.Columns.Count} index values, received {values.Count}.", nameof(values));
        var output = new ArrayBufferWriter<byte>();
        for (var indexOffset = 0; indexOffset < index.Columns.Count; indexOffset++)
        {
            var indexed = index.Columns[indexOffset];
            var column = table.Columns.Single(candidate => candidate.Id == indexed.ColumnId);
            var value = values[indexOffset] ?? throw new ArgumentException("Index values cannot contain null CLR references.", nameof(values));
            if (!value.IsNull && value.Type != column.Type)
                throw new ArgumentException($"Column '{column.Name}' expects {column.Type}, received {value.Type}.", nameof(values));
            var segment = new ArrayBufferWriter<byte>();
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
                    case DecimalSqlValue exact: WriteDecimal(segment, exact.Value); break;
                    case FloatSqlValue approximate: WriteSortableDouble(segment, approximate.Value); break;
                    case DateSqlValue date: WriteSortableInt64(segment, date.Value.DayNumber); break;
                    case TimeSqlValue time: WriteSortableInt64(segment, time.Value.Ticks); break;
                    case DateTimeSqlValue dateTime: WriteSortableInt64(segment, dateTime.Value.Ticks); break;
                    case DateTimeOffsetSqlValue dateTimeOffset: WriteSortableInt64(segment, dateTimeOffset.Value.UtcTicks); break;
                    case UniqueIdentifierSqlValue identifier:
                        var identifierBytes = new byte[16];
                        identifier.Value.TryWriteBytes(identifierBytes, bigEndian: true, out _);
                        Write(segment, identifierBytes);
                        break;
                    default: throw new ArgumentException("Unsupported indexed SQL value.", nameof(values));
                }
            }
            var encodedSegment = segment.WrittenSpan.ToArray();
            if (indexed.Direction == SortDirection.Descending)
                for (var offset = 0; offset < encodedSegment.Length; offset++) encodedSegment[offset] ^= 0xFF;
            Write(output, encodedSegment);
        }
        return new IndexKey(output.WrittenSpan);
    }

    private static void ValidateFor(this CatalogTable table, CatalogIndex index)
    {
        if (index.TableId != table.Id) throw new ArgumentException("Index does not belong to the table.", nameof(index));
    }

    private static void WriteLengthBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
        Write(output, length);
        Write(output, bytes);
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

    private static void WriteDecimal(IBufferWriter<byte> output, decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = ((BigInteger)(uint)bits[2] << 64) |
                          ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        var scale = (bits[3] >> 16) & 0xFF;
        var magnitude = coefficient * BigInteger.Pow(10, 28 - scale);
        Span<byte> bytes = stackalloc byte[25];
        bytes.Clear();
        var negative = (bits[3] & int.MinValue) != 0 && !coefficient.IsZero;
        bytes[0] = negative ? (byte)0x7F : (byte)0x80;
        var magnitudeLength = magnitude.GetByteCount(isUnsigned: true);
        if (!magnitude.TryWriteBytes(bytes[(bytes.Length - magnitudeLength)..], out _, isUnsigned: true, isBigEndian: true))
            throw new InvalidOperationException("SQL decimal did not fit its canonical index representation.");
        if (negative)
            for (var index = 1; index < bytes.Length; index++) bytes[index] ^= 0xFF;
        Write(output, bytes);
    }
    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value) { value.CopyTo(output.GetSpan(value.Length)); output.Advance(value.Length); }
}

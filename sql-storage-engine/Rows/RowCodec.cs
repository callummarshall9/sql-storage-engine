using System.Buffers.Binary;
using System.Text;
using sql_storage_engine.Overflow;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

public enum RowValueStorage : ushort
{
    Null = 0,
    Inline = 1,
    Overflow = 2
}

/// <summary>Encodes version-one typed rows without runtime object serialization.</summary>
public static class RowCodec
{
    public const ushort FormatVersion = 1;
    public const int HeaderLength = 32;
    public const int MaximumEncodedLength = 16 * 1024 * 1024;
    public const int MaximumInlineValueLength = 1024 * 1024;
    public const int VariableEntryLength = 12;
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Encode(Row row, TableDefinition table)
        => EncodeCore(row, table, null);

    internal static byte[] EncodeCore(Row row, TableDefinition table,
        IReadOnlyDictionary<int, OverflowReference>? overflowReferences)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(table);
        table.ValidateRow(row);
        if (table.Columns.Count > ushort.MaxValue) throw new ArgumentException("Schema has too many columns.", nameof(table));

        var nullBytes = checked((table.Columns.Count + 7) / 8);
        var fixedLength = table.Columns.Sum(GetFixedWidth);
        var variableColumns = table.Columns.Select((column, index) => (column, index))
            .Where(item => item.column.Type is SqlType.Text or SqlType.Binary).ToArray();
        var variableValues = new (RowValueStorage Storage, byte[] Bytes)[variableColumns.Length];
        var variableLength = 0;
        for (var variableIndex = 0; variableIndex < variableColumns.Length; variableIndex++)
        {
            var value = row.Values[variableColumns[variableIndex].index];
            var columnIndex = variableColumns[variableIndex].index;
            RowValueStorage storage;
            byte[] encodedValue;
            if (overflowReferences is not null && overflowReferences.TryGetValue(columnIndex, out var reference))
            {
                OverflowReferenceCodec.Validate(reference);
                storage = RowValueStorage.Overflow;
                encodedValue = new byte[OverflowReferenceCodec.EncodedLength];
                OverflowReferenceCodec.Write(encodedValue, reference);
            }
            else
            {
                storage = value.IsNull ? RowValueStorage.Null : RowValueStorage.Inline;
                encodedValue = value switch
                {
                    NullSqlValue => [],
                    TextSqlValue text => Utf8.GetBytes(text.Value),
                    BinarySqlValue binary => binary.Value.ToArray(),
                    _ => throw new ArgumentException("Variable column has an invalid value type.", nameof(row))
                };
            }
            if (storage == RowValueStorage.Inline && encodedValue.Length > MaximumInlineValueLength)
                throw new ArgumentException($"Inline value exceeds {MaximumInlineValueLength} bytes.", nameof(row));
            variableValues[variableIndex] = (storage, encodedValue);
            variableLength = checked(variableLength + encodedValue.Length);
        }
        var variableTableOffset = checked(HeaderLength + nullBytes + fixedLength);
        var variableDataOffset = checked(variableTableOffset + variableColumns.Length * VariableEntryLength);
        var totalLength = checked(variableDataOffset + variableLength);
        if (totalLength > MaximumEncodedLength) throw new ArgumentException("Encoded row exceeds the maximum size.", nameof(row));
        var bytes = new byte[totalLength];
        WriteHeader(bytes, table, checked((ushort)nullBytes), checked((ushort)variableColumns.Length), checked((uint)fixedLength),
            checked((uint)variableTableOffset), checked((uint)variableDataOffset), checked((uint)totalLength));
        var fixedOffset = HeaderLength + nullBytes;
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var value = row.Values[index];
            if (value.IsNull)
                bytes[HeaderLength + index / 8] |= checked((byte)(1 << (index % 8)));
            else if (column.Type == SqlType.Boolean)
                bytes[fixedOffset] = ((BooleanSqlValue)value).Value ? (byte)1 : (byte)0;
            else if (column.Type == SqlType.Integer)
                BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(fixedOffset), ((IntegerSqlValue)value).Value);
            fixedOffset += GetFixedWidth(column);
        }
        var cursor = variableDataOffset;
        for (var variableIndex = 0; variableIndex < variableColumns.Length; variableIndex++)
        {
            var entry = bytes.AsSpan(variableTableOffset + variableIndex * VariableEntryLength, VariableEntryLength);
            BinaryPrimitives.WriteUInt16LittleEndian(entry, checked((ushort)variableColumns[variableIndex].index));
            BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], (ushort)variableValues[variableIndex].Storage);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], checked((uint)cursor));
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], checked((uint)variableValues[variableIndex].Bytes.Length));
            variableValues[variableIndex].Bytes.CopyTo(bytes.AsSpan(cursor));
            cursor = checked(cursor + variableValues[variableIndex].Bytes.Length);
        }
        return bytes;
    }

    public static Row Decode(ReadOnlySpan<byte> source, TableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var header = ReadAndValidateHeader(source, table);
        var variables = ReadVariableEntries(source, table, header);
        var values = new SqlValue[table.Columns.Count];
        var fixedOffset = checked(HeaderLength + header.NullBitmapLength);
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var isNull = (source[HeaderLength + index / 8] & (1 << (index % 8))) != 0;
            if (isNull)
            {
                if (!column.IsNullable) throw new StorageFormatException($"Non-nullable column '{column.Name}' is encoded as NULL.");
                values[index] = SqlValue.Null;
            }
            else if (column.Type == SqlType.Boolean)
            {
                values[index] = source[fixedOffset] switch
                {
                    0 => SqlValue.Boolean(false),
                    1 => SqlValue.Boolean(true),
                    _ => throw new StorageFormatException($"Invalid boolean byte for column '{column.Name}'.")
                };
            }
            else if (column.Type == SqlType.Integer)
            {
                values[index] = SqlValue.Integer(BinaryPrimitives.ReadInt64LittleEndian(source[fixedOffset..]));
            }
            else if (column.Type == SqlType.Text)
            {
                var entry = variables[index];
                if (entry.Storage == RowValueStorage.Overflow)
                    throw new StorageFormatException("Overflow row value requires OverflowRowCodec.");
                try { values[index] = SqlValue.Text(Utf8.GetString(source.Slice(entry.Offset, entry.Length))); }
                catch (DecoderFallbackException exception) { throw new StorageFormatException($"Column '{column.Name}' contains invalid UTF-8.", exception); }
            }
            else
            {
                var entry = variables[index];
                if (entry.Storage == RowValueStorage.Overflow)
                    throw new StorageFormatException("Overflow row value requires OverflowRowCodec.");
                values[index] = SqlValue.Binary(source.Slice(entry.Offset, entry.Length));
            }
            fixedOffset = checked(fixedOffset + GetFixedWidth(column));
        }
        return new Row(values);
    }

    /// <summary>Validates and atomically applies selected logical column replacements.</summary>
    public static byte[] ApplyUpdate(ReadOnlySpan<byte> currentRow, RowUpdate update, TableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(table);
        var current = Decode(currentRow, table);
        var indexes = new HashSet<int>();
        foreach (var columnUpdate in update.Columns)
        {
            if (columnUpdate.ColumnIndex < 0 || columnUpdate.ColumnIndex >= table.Columns.Count)
                throw new ArgumentOutOfRangeException(nameof(update), $"Unknown column index {columnUpdate.ColumnIndex}.");
            if (!indexes.Add(columnUpdate.ColumnIndex))
                throw new ArgumentException($"Column index {columnUpdate.ColumnIndex} is updated more than once.", nameof(update));
            table.Columns[columnUpdate.ColumnIndex].Validate(columnUpdate.Value);
        }

        var values = current.Values.ToArray();
        foreach (var columnUpdate in update.Columns) values[columnUpdate.ColumnIndex] = columnUpdate.Value;
        return Encode(new Row(values), table);
    }

    /// <summary>Decodes requested columns after validating the complete encoded layout.</summary>
    public static IReadOnlyDictionary<ColumnId, SqlValue> DecodeSelected(ReadOnlySpan<byte> source,
        TableDefinition table, IEnumerable<ColumnId> columnIds)
    {
        ArgumentNullException.ThrowIfNull(columnIds);
        var requested = columnIds.ToArray();
        if (requested.Distinct().Count() != requested.Length) throw new ArgumentException("Requested columns must be unique.", nameof(columnIds));
        var requestedSet = requested.ToHashSet();
        if (requestedSet.Any(id => table.Columns.All(column => column.Id != id)))
            throw new ArgumentException("Requested column is not present in the schema.", nameof(columnIds));
        var header = ReadAndValidateHeader(source, table);
        var variables = ReadVariableEntries(source, table, header);
        Dictionary<ColumnId, SqlValue> result = [];
        var fixedOffset = HeaderLength + header.NullBitmapLength;
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            if (requestedSet.Contains(column.Id))
                result.Add(column.Id, DecodeColumn(source, column, index, fixedOffset, variables));
            fixedOffset = checked(fixedOffset + GetFixedWidth(column));
        }
        return result;
    }

    internal static RowHeader ReadAndValidateHeader(ReadOnlySpan<byte> source, TableDefinition table)
    {
        if (source.Length < HeaderLength) throw new StorageFormatException("Row header is truncated.");
        var header = new RowHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(source),
            BinaryPrimitives.ReadUInt16LittleEndian(source[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(source[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(source[6..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[24..]));
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[28..]) != 0)
            throw new StorageFormatException("Reserved row-header bytes must be zero.");
        if (header.Version != FormatVersion) throw new StorageFormatException($"Unsupported row format version {header.Version}.");
        if (header.ColumnCount != table.Columns.Count) throw new StorageFormatException("Encoded row column count does not match schema.");
        if (header.SchemaHash != CalculateSchemaHash(table)) throw new StorageFormatException("Encoded row schema fingerprint does not match.");
        var expectedNullBytes = checked((table.Columns.Count + 7) / 8);
        if (header.NullBitmapLength != expectedNullBytes) throw new StorageFormatException("Invalid row null-bitmap length.");
        if (header.TotalLength > MaximumEncodedLength || header.TotalLength != source.Length)
            throw new StorageFormatException("Encoded row total length is invalid or truncated.");
        var fixedStart = checked((uint)(HeaderLength + expectedNullBytes));
        var fixedEnd = checked(fixedStart + header.FixedDataLength);
        var expectedVariableCount = table.Columns.Count(column => column.Type is SqlType.Text or SqlType.Binary);
        if (header.VariableCount != expectedVariableCount) throw new StorageFormatException("Variable-column count does not match schema.");
        var expectedVariableDataOffset = checked(fixedEnd + (uint)(expectedVariableCount * VariableEntryLength));
        if (header.VariableTableOffset != fixedEnd || header.VariableDataOffset != expectedVariableDataOffset ||
            header.VariableDataOffset > header.TotalLength)
            throw new StorageFormatException("Encoded row region offsets are invalid.");
        var expectedFixedLength = table.Columns.Sum(GetFixedWidth);
        if (header.FixedDataLength != expectedFixedLength) throw new StorageFormatException("Fixed row region does not match schema.");
        var unusedNullBits = expectedNullBytes * 8 - table.Columns.Count;
        if (unusedNullBits > 0)
        {
            var validMask = (1 << (8 - unusedNullBits)) - 1;
            if ((source[HeaderLength + expectedNullBytes - 1] & ~validMask) != 0)
                throw new StorageFormatException("Unused null-bitmap bits must be zero.");
        }
        return header;
    }

    internal static Dictionary<int, VariableEntry> ReadVariableEntries(ReadOnlySpan<byte> source,
        TableDefinition table, RowHeader header)
    {
        Dictionary<int, VariableEntry> entries = [];
        var expectedColumnIndexes = table.Columns.Select((column, index) => (column, index))
            .Where(item => item.column.Type is SqlType.Text or SqlType.Binary).Select(item => item.index).ToArray();
        var cursor = checked((int)header.VariableDataOffset);
        for (var variableIndex = 0; variableIndex < expectedColumnIndexes.Length; variableIndex++)
        {
            var entryOffset = checked((int)header.VariableTableOffset + variableIndex * VariableEntryLength);
            var entry = source.Slice(entryOffset, VariableEntryLength);
            var columnIndex = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            var storage = (RowValueStorage)BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
            if (!Enum.IsDefined(storage)) throw new StorageFormatException("Unknown variable-value storage tag.");
            if (columnIndex != expectedColumnIndexes[variableIndex])
                throw new StorageFormatException("Variable offset table does not follow schema order.");
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            var isNull = (source[HeaderLength + columnIndex / 8] & (1 << (columnIndex % 8))) != 0;
            if (isNull != (storage == RowValueStorage.Null) ||
                storage == RowValueStorage.Null && length != 0 ||
                storage == RowValueStorage.Overflow && length != OverflowReferenceCodec.EncodedLength ||
                storage == RowValueStorage.Inline && length > MaximumInlineValueLength ||
                offset != cursor || checked((ulong)offset + length) > header.TotalLength)
                throw new StorageFormatException("Variable value offsets overlap, decrease, or exceed the row.");
            entries.Add(columnIndex, new VariableEntry(storage, checked((int)offset), checked((int)length)));
            cursor = checked((int)(offset + length));
        }
        if (cursor != header.TotalLength) throw new StorageFormatException("Variable values do not cover the declared data region.");
        return entries;
    }

    private static SqlValue DecodeColumn(ReadOnlySpan<byte> source, ColumnDefinition column, int columnIndex,
        int fixedOffset, IReadOnlyDictionary<int, VariableEntry> variables)
    {
        var isNull = (source[HeaderLength + columnIndex / 8] & (1 << (columnIndex % 8))) != 0;
        if (isNull)
        {
            if (!column.IsNullable) throw new StorageFormatException($"Non-nullable column '{column.Name}' is encoded as NULL.");
            return SqlValue.Null;
        }
        return column.Type switch
        {
            SqlType.Boolean => source[fixedOffset] switch
            {
                0 => SqlValue.Boolean(false), 1 => SqlValue.Boolean(true),
                _ => throw new StorageFormatException($"Invalid boolean byte for column '{column.Name}'.")
            },
            SqlType.Integer => SqlValue.Integer(BinaryPrimitives.ReadInt64LittleEndian(source[fixedOffset..])),
            SqlType.Text when variables[columnIndex].Storage == RowValueStorage.Inline => DecodeText(source, variables[columnIndex], column.Name),
            SqlType.Binary when variables[columnIndex].Storage == RowValueStorage.Inline => SqlValue.Binary(source.Slice(variables[columnIndex].Offset, variables[columnIndex].Length)),
            SqlType.Text or SqlType.Binary => throw new StorageFormatException("Overflow row value requires OverflowRowCodec."),
            _ => throw new StorageFormatException("Unknown SQL column type.")
        };
    }

    internal static SqlValue DecodeText(ReadOnlySpan<byte> source, VariableEntry entry, string columnName)
    {
        try { return SqlValue.Text(Utf8.GetString(source.Slice(entry.Offset, entry.Length))); }
        catch (DecoderFallbackException exception) { throw new StorageFormatException($"Column '{columnName}' contains invalid UTF-8.", exception); }
    }

    internal static int GetFixedWidth(ColumnDefinition column) => column.Type switch
    {
        SqlType.Boolean => 1,
        SqlType.Integer => 8,
        SqlType.Text or SqlType.Binary => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(column))
    };

    internal static uint CalculateSchemaHash(TableDefinition table)
    {
        var hash = 2166136261u;
        foreach (var column in table.Columns)
        {
            var id = column.Id.Value;
            for (var shift = 0; shift < 64; shift += 8) { hash ^= (byte)(id >> shift); hash *= 16777619u; }
            hash ^= (byte)column.Type; hash *= 16777619u;
            hash ^= column.IsNullable ? (byte)1 : (byte)0; hash *= 16777619u;
        }
        return hash;
    }

    internal static void WriteHeader(Span<byte> destination, TableDefinition table, ushort nullBytes,
        ushort variableCount, uint fixedLength, uint variableTableOffset, uint variableDataOffset, uint totalLength)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], checked((ushort)table.Columns.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], nullBytes);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], variableCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], fixedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], variableTableOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], variableDataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[20..], totalLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[24..], CalculateSchemaHash(table));
    }

    internal readonly record struct RowHeader(ushort Version, ushort ColumnCount, ushort NullBitmapLength,
        ushort VariableCount, uint FixedDataLength, uint VariableTableOffset, uint VariableDataOffset,
        uint TotalLength, uint SchemaHash);

    internal readonly record struct VariableEntry(RowValueStorage Storage, int Offset, int Length);
}

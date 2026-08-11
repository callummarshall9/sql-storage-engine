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
    public const ushort FormatVersion = 4;
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
            .Where(item => IsVariable(item.column) &&
                           (!IsOmittable(item.column) || !row.Values[item.index].IsNull)).ToArray();
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
                encodedValue = value.IsNull ? [] : EncodeVariableValue(value, variableColumns[variableIndex].column);
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
            else if (!IsVariable(column))
                WriteFixedValue(bytes.AsSpan(fixedOffset), value, column);
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
            else if (!IsVariable(column))
                values[index] = DecodeFixedValue(source[fixedOffset..], column);
            else
            {
                var entry = variables[index];
                if (entry.Storage == RowValueStorage.Overflow)
                    throw new StorageFormatException("Overflow row value requires OverflowRowCodec.");
                values[index] = DecodeVariableValue(source.Slice(entry.Offset, entry.Length), column);
            }
            ValidateDecodedValue(values[index], column);
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
        var minimumVariableCount = table.Columns.Count(column => IsVariable(column) && !IsOmittable(column));
        var maximumVariableCount = table.Columns.Count(IsVariable);
        if (header.VariableCount < minimumVariableCount || header.VariableCount > maximumVariableCount)
            throw new StorageFormatException("Variable-column count does not match schema.");
        var expectedVariableDataOffset = checked(fixedEnd + (uint)(header.VariableCount * VariableEntryLength));
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
        var eligibleColumnIndexes = table.Columns.Select((column, index) => (column, index))
            .Where(item => IsVariable(item.column)).Select(item => item.index).ToHashSet();
        var cursor = checked((int)header.VariableDataOffset);
        var previousColumnIndex = -1;
        for (var variableIndex = 0; variableIndex < header.VariableCount; variableIndex++)
        {
            var entryOffset = checked((int)header.VariableTableOffset + variableIndex * VariableEntryLength);
            var entry = source.Slice(entryOffset, VariableEntryLength);
            var columnIndex = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            var storage = (RowValueStorage)BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);
            if (!Enum.IsDefined(storage)) throw new StorageFormatException("Unknown variable-value storage tag.");
            if (!eligibleColumnIndexes.Contains(columnIndex) || columnIndex <= previousColumnIndex)
                throw new StorageFormatException("Variable offset table does not follow schema order.");
            previousColumnIndex = columnIndex;
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            var isNull = (source[HeaderLength + columnIndex / 8] & (1 << (columnIndex % 8))) != 0;
            var column = table.Columns[columnIndex];
            if (isNull != (storage == RowValueStorage.Null) ||
                IsOmittable(column) && isNull ||
                storage == RowValueStorage.Null && length != 0 ||
                storage == RowValueStorage.Overflow && length != OverflowReferenceCodec.EncodedLength ||
                storage == RowValueStorage.Inline && length > MaximumInlineValueLength ||
                offset != cursor || checked((ulong)offset + length) > header.TotalLength)
                throw new StorageFormatException("Variable value offsets overlap, decrease, or exceed the row.");
            entries.Add(columnIndex, new VariableEntry(storage, checked((int)offset), checked((int)length)));
            cursor = checked((int)(offset + length));
        }
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index]; if (!IsVariable(column)) continue;
            var isNull = (source[HeaderLength + index / 8] & (1 << (index % 8))) != 0;
            if (!IsOmittable(column) && !entries.ContainsKey(index) || IsOmittable(column) && !isNull && !entries.ContainsKey(index))
                throw new StorageFormatException("Variable offset table omits a required value.");
            if (IsOmittable(column) && isNull && entries.ContainsKey(index))
                throw new StorageFormatException("A NULL sparse or virtual column must not have a variable entry.");
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
        SqlValue value;
        if (!IsVariable(column)) value = DecodeFixedValue(source[fixedOffset..], column);
        else
        {
            var entry = variables[columnIndex];
            if (entry.Storage != RowValueStorage.Inline)
                throw new StorageFormatException("Overflow row value requires OverflowRowCodec.");
            value = DecodeVariableValue(source.Slice(entry.Offset, entry.Length), column);
        }
        ValidateDecodedValue(value, column);
        return value;
    }

    internal static bool IsVariable(SqlType type) => type.Name switch
    {
        SqlTypeName.Bit or SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt or
            SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money or
            SqlTypeName.Real or SqlTypeName.Float or SqlTypeName.Date or SqlTypeName.Time or
            SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 or
            SqlTypeName.DateTimeOffset or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.UniqueIdentifier => false,
        _ => true
    };

    internal static bool IsVariable(ColumnDefinition column) => column.IsSparse || column.IsColumnSet ||
        column.Encryption is not null || IsVariable(column.Type);

    internal static bool IsOmittable(ColumnDefinition column) => column.IsSparse || column.IsColumnSet;

    internal static int GetFixedWidth(ColumnDefinition column) => column.IsSparse || column.IsColumnSet || column.Encryption is not null ? 0 : column.Type.Name switch
    {
        SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
        SqlTypeName.SmallInt => 2,
        SqlTypeName.Int or SqlTypeName.SmallMoney or SqlTypeName.Real or SqlTypeName.Date => 4,
        SqlTypeName.Float when column.Type.Precision <= 24 => 4,
        SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.Float or SqlTypeName.Time or
            SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => 8,
        SqlTypeName.Decimal or SqlTypeName.Numeric => GetDecimalWidth(column.Type),
        SqlTypeName.DateTimeOffset => 10,
        SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
        SqlTypeName.UniqueIdentifier => 16,
        _ => 0
    };

    internal static void WriteFixedValue(Span<byte> destination, SqlValue value, ColumnDefinition column)
    {
        value = column.Type.NormalizeForStorage(value);
        switch (value)
        {
            case BooleanSqlValue boolean:
                destination[0] = boolean.Value ? (byte)1 : (byte)0;
                break;
            case IntegerSqlValue integer:
                WriteInteger(destination, integer.Value, column.Type.Name);
                break;
            case DecimalSqlValue exact:
                WriteDecimal(destination, exact.Value, column.Type);
                break;
            case FloatSqlValue approximate when column.Type.Name == SqlTypeName.Real ||
                                                column.Type.Name == SqlTypeName.Float && column.Type.Precision <= 24:
                BinaryPrimitives.WriteSingleLittleEndian(destination, (float)approximate.Value);
                break;
            case FloatSqlValue approximate:
                BinaryPrimitives.WriteDoubleLittleEndian(destination, approximate.Value);
                break;
            case DateSqlValue date:
                BinaryPrimitives.WriteInt32LittleEndian(destination, date.Value.DayNumber);
                break;
            case TimeSqlValue time:
                BinaryPrimitives.WriteInt64LittleEndian(destination,
                    RoundTemporalTicks(time.Value.Ticks, column.Type.Scale!.Value) % TimeSpan.TicksPerDay);
                break;
            case DateTimeSqlValue dateTime:
                BinaryPrimitives.WriteInt64LittleEndian(destination, RoundDateTime(dateTime.Value, column.Type).Ticks);
                break;
            case DateTimeOffsetSqlValue dateTimeOffset:
                BinaryPrimitives.WriteInt64LittleEndian(destination,
                    RoundTemporalTicks(dateTimeOffset.Value.UtcTicks, column.Type.Scale!.Value));
                BinaryPrimitives.WriteInt16LittleEndian(destination[8..], checked((short)dateTimeOffset.Value.Offset.TotalMinutes));
                break;
            case BinarySqlValue binary when column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp:
                binary.Value.Span.CopyTo(destination);
                break;
            case UniqueIdentifierSqlValue identifier:
                if (!identifier.Value.TryWriteBytes(destination, bigEndian: true, out var bytesWritten) || bytesWritten != 16)
                    throw new InvalidOperationException("Could not encode SQL unique identifier.");
                break;
            default:
                throw new ArgumentException("Value is not a supported fixed-width SQL value.", nameof(value));
        }
    }

    private static long RoundTemporalTicks(long ticks, byte scale)
    {
        var quantum = (long)Math.Pow(10, 7 - scale);
        var remainder = ticks % quantum;
        if (remainder == 0) return ticks;
        return checked(ticks - remainder + (remainder * 2 >= quantum ? quantum : 0));
    }

    private static DateTime RoundDateTime(DateTime value, SqlType type)
    {
        if (type.Name == SqlTypeName.SmallDateTime)
        {
            var minuteTicks = value.Ticks - value.Ticks % TimeSpan.TicksPerMinute;
            if (value.Ticks - minuteTicks >= TimeSpan.TicksPerSecond * 30) minuteTicks += TimeSpan.TicksPerMinute;
            return new DateTime(minuteTicks, DateTimeKind.Unspecified);
        }
        if (type.Name == SqlTypeName.DateTime)
        {
            var dayTicks = value.Date.Ticks;
            var units = (long)Math.Round(value.TimeOfDay.TotalSeconds * 300d, MidpointRounding.AwayFromZero);
            var rounded = checked(dayTicks + (long)Math.Round(units * (double)TimeSpan.TicksPerSecond / 300d,
                MidpointRounding.AwayFromZero));
            return new DateTime(rounded, DateTimeKind.Unspecified);
        }
        return new DateTime(RoundTemporalTicks(value.Ticks, type.Scale!.Value), DateTimeKind.Unspecified);
    }

    internal static SqlValue DecodeFixedValue(ReadOnlySpan<byte> source, ColumnDefinition column)
    {
        try
        {
            return column.Type.Name switch
            {
                SqlTypeName.Bit => source[0] switch
                {
                    0 => SqlValue.Boolean(false), 1 => SqlValue.Boolean(true),
                    _ => throw new StorageFormatException($"Invalid boolean byte for column '{column.Name}'.")
                },
                SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt =>
                    SqlValue.Integer(ReadInteger(source, column.Type.Name)),
                SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money =>
                    DecodeDecimal(source, column.Type),
                SqlTypeName.Real => SqlValue.Float(BinaryPrimitives.ReadSingleLittleEndian(source)),
                SqlTypeName.Float when column.Type.Precision <= 24 =>
                    SqlValue.Float(BinaryPrimitives.ReadSingleLittleEndian(source)),
                SqlTypeName.Float => SqlValue.Float(BinaryPrimitives.ReadDoubleLittleEndian(source)),
                SqlTypeName.Date => SqlValue.Date(DateOnly.FromDayNumber(BinaryPrimitives.ReadInt32LittleEndian(source))),
                SqlTypeName.Time => SqlValue.Time(new TimeOnly(BinaryPrimitives.ReadInt64LittleEndian(source))),
                SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 =>
                    SqlValue.DateTime(new System.DateTime(BinaryPrimitives.ReadInt64LittleEndian(source), DateTimeKind.Unspecified)),
                SqlTypeName.DateTimeOffset => DecodeDateTimeOffset(source),
                SqlTypeName.RowVersion or SqlTypeName.Timestamp => SqlValue.Binary(source[..8]),
                SqlTypeName.UniqueIdentifier => SqlValue.UniqueIdentifier(new Guid(source[..16], bigEndian: true)),
                _ => throw new StorageFormatException("Unknown fixed-width SQL column type.")
            };
        }
        catch (StorageFormatException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new StorageFormatException($"Column '{column.Name}' contains an invalid {column.Type} value.", exception);
        }
    }

    private static SqlValue DecodeDecimal(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            var scaled = type.Name == SqlTypeName.Money
                ? new System.Numerics.BigInteger(BinaryPrimitives.ReadInt64LittleEndian(source))
                : new System.Numerics.BigInteger(BinaryPrimitives.ReadInt32LittleEndian(source));
            return SqlValue.Decimal(scaled, 4);
        }
        var negative = source[0] switch
        {
            0 => false, 1 => true,
            _ => throw new StorageFormatException("Invalid SQL decimal sign byte.")
        };
        var coefficient = new System.Numerics.BigInteger(source[1..GetDecimalWidth(type)], isUnsigned: true,
            isBigEndian: false);
        return SqlValue.Decimal(negative ? -coefficient : coefficient, type.Scale!.Value);
    }

    internal static byte[] EncodeVariableValue(SqlValue value, ColumnDefinition column)
    {
        value = column.Type.NormalizeForStorage(value);
        var plaintext = EncodeLogicalValue(value, column);
        return column.Encryption is null ? plaintext : SqlColumnEncryption.Encrypt(column.Encryption, plaintext);
    }

    internal static byte[] EncodeLogicalValue(SqlValue value, ColumnDefinition column) =>
        IsVariable(column.Type) ? EncodeUnencryptedVariable(value, column) : EncodeFixedForEncryption(value, column);

    private static byte[] EncodeUnencryptedVariable(SqlValue value, ColumnDefinition column) => value switch
        {
            TextSqlValue text => EncodeText(text.Value, column.Type),
            BinarySqlValue binary => EncodeBinary(binary.Value.Span, column.Type),
            HierarchyIdSqlValue hierarchy => hierarchy.Value.Serialize(),
            SpatialSqlValue spatial => spatial.Value.Serialize(),
            VectorSqlValue vector => EncodeVector(vector),
            VariantSqlValue variant => EncodeVariant(variant),
            _ => throw new ArgumentException($"Column '{column.Name}' has an invalid variable value.", nameof(value))
        };

    internal static SqlValue DecodeVariableValue(ReadOnlySpan<byte> source, ColumnDefinition column)
    {
        try
        {
            var plaintext = column.Encryption is null ? source.ToArray() : SqlColumnEncryption.Decrypt(column.Encryption, source);
            if (!IsVariable(column.Type)) return DecodeFixedValue(plaintext,
                new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable));
            source = plaintext;
            return column.Type.StorageFamily switch
            {
                SqlStorageFamily.Text => SqlValue.Text(DecodeText(source, column.Type)),
                SqlStorageFamily.Binary when column.Type.Name == SqlTypeName.HierarchyId =>
                    new HierarchyIdSqlValue(SqlHierarchyId.Deserialize(source)),
                SqlStorageFamily.Binary when column.Type.Name == SqlTypeName.Geometry =>
                    new SpatialSqlValue(SqlSpatial.Deserialize(source, SqlSpatialKind.Geometry)),
                SqlStorageFamily.Binary when column.Type.Name == SqlTypeName.Geography =>
                    new SpatialSqlValue(SqlSpatial.Deserialize(source, SqlSpatialKind.Geography)),
                SqlStorageFamily.Binary => SqlValue.Binary(source),
                SqlStorageFamily.Vector => DecodeVector(source, column.Type),
                SqlStorageFamily.Variant => DecodeVariant(source),
                _ => throw new StorageFormatException($"Column '{column.Name}' is not variable-width.")
            };
        }
        catch (StorageFormatException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or DecoderFallbackException or OverflowException)
        {
            throw new StorageFormatException($"Column '{column.Name}' contains an invalid {column.Type} value.", exception);
        }
    }

    private static byte[] EncodeFixedForEncryption(SqlValue value, ColumnDefinition column)
    {
        var plaintextColumn = new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable);
        var bytes = new byte[GetFixedWidth(plaintextColumn)]; WriteFixedValue(bytes, value, plaintextColumn); return bytes;
    }

    private static byte[] EncodeText(string value, SqlType type)
    {
        if (type.Name == SqlTypeName.Json) return SqlJsonBinaryCodec.Encode(value);
        if (type.Name == SqlTypeName.Xml) return SqlXmlBinaryCodec.Encode(value);
        byte[] bytes;
        if (type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText)
            bytes = new UnicodeEncoding(false, false, true).GetBytes(value);
        else
            bytes = (type.CollationMetadata ?? SqlCollation.Default).Encode(value);
        if (type.Name is not (SqlTypeName.Char or SqlTypeName.NChar) || type.Length is not { } declared) return bytes;
        var targetLength = type.Name == SqlTypeName.NChar ? checked(declared * 2) : declared;
        if (bytes.Length == targetLength) return bytes;
        var padded = new byte[targetLength];
        bytes.CopyTo(padded, 0);
        if (type.Name == SqlTypeName.NChar)
            for (var offset = bytes.Length; offset < padded.Length; offset += 2) padded[offset] = 0x20;
        else
            padded.AsSpan(bytes.Length).Fill(0x20);
        return padded;
    }

    private static string DecodeText(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.Name == SqlTypeName.Json) return SqlJsonBinaryCodec.Decode(source);
        if (type.Name == SqlTypeName.Xml) return SqlXmlBinaryCodec.Decode(source);
        return type.Name is SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText
            ? new UnicodeEncoding(false, false, true).GetString(source)
            : (type.CollationMetadata ?? SqlCollation.Default).Decode(source);
    }

    private static byte[] EncodeBinary(ReadOnlySpan<byte> value, SqlType type)
    {
        if (type.Name != SqlTypeName.Binary || type.Length is not { } declared || value.Length == declared)
            return value.ToArray();
        var padded = new byte[declared];
        value.CopyTo(padded);
        return padded;
    }

    internal static void ValidateDecodedValue(SqlValue value, ColumnDefinition column)
    {
        try { column.Validate(value); }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException($"Column '{column.Name}' violates its declared {column.Type} facets.", exception);
        }
    }

    private static void WriteInteger(Span<byte> destination, long value, SqlTypeName type)
    {
        switch (type)
        {
            case SqlTypeName.TinyInt: destination[0] = checked((byte)value); break;
            case SqlTypeName.SmallInt: BinaryPrimitives.WriteInt16LittleEndian(destination, checked((short)value)); break;
            case SqlTypeName.Int: BinaryPrimitives.WriteInt32LittleEndian(destination, checked((int)value)); break;
            case SqlTypeName.BigInt: BinaryPrimitives.WriteInt64LittleEndian(destination, value); break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static long ReadInteger(ReadOnlySpan<byte> source, SqlTypeName type) => type switch
    {
        SqlTypeName.TinyInt => source[0],
        SqlTypeName.SmallInt => BinaryPrimitives.ReadInt16LittleEndian(source),
        SqlTypeName.Int => BinaryPrimitives.ReadInt32LittleEndian(source),
        SqlTypeName.BigInt => BinaryPrimitives.ReadInt64LittleEndian(source),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static int GetDecimalWidth(SqlType type) => type.Precision switch
    {
        <= 9 => 5, <= 19 => 9, <= 28 => 13, _ => 17
    };

    private static void WriteDecimal(Span<byte> destination, SqlDecimal value, SqlType type)
    {
        if (type.Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            value.RoundToScale(4, out var scaled);
            if (type.Name == SqlTypeName.Money)
                BinaryPrimitives.WriteInt64LittleEndian(destination, checked((long)scaled));
            else
                BinaryPrimitives.WriteInt32LittleEndian(destination, checked((int)scaled));
            return;
        }
        value.RoundToScale(type.Scale!.Value, out var coefficient);
        destination[0] = coefficient.Sign < 0 ? (byte)1 : (byte)0;
        destination[1..GetDecimalWidth(type)].Clear();
        if (!System.Numerics.BigInteger.Abs(coefficient).TryWriteBytes(destination[1..GetDecimalWidth(type)],
                out _, isUnsigned: true, isBigEndian: false))
            throw new ArgumentException("Decimal coefficient does not fit the declared precision.");
    }

    private static byte[] EncodeVector(VectorSqlValue vector)
    {
        var elementWidth = vector.BaseType == SqlVectorBaseType.Float32 ? 4 : 2;
        var bytes = new byte[checked(vector.Values.Count * elementWidth)];
        if (vector.BaseType == SqlVectorBaseType.Float32)
            for (var index = 0; index < vector.Values.Count; index++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 4), vector.Values[index]);
        else
            for (var index = 0; index < vector.HalfValues.Count; index++)
                BinaryPrimitives.WriteHalfLittleEndian(bytes.AsSpan(index * 2), vector.HalfValues[index]);
        return bytes;
    }

    private static SqlValue DecodeVector(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.VectorBaseType == SqlVectorBaseType.Float32)
        {
            if (source.Length != type.VectorDimensions * 4) throw new StorageFormatException("Invalid float32 vector length.");
            var values = new float[type.VectorDimensions!.Value];
            for (var index = 0; index < values.Length; index++)
                values[index] = BinaryPrimitives.ReadSingleLittleEndian(source[(index * 4)..]);
            return SqlValue.Vector(values);
        }
        if (source.Length != type.VectorDimensions * 2) throw new StorageFormatException("Invalid float16 vector length.");
        var halves = new Half[type.VectorDimensions!.Value];
        for (var index = 0; index < halves.Length; index++)
            halves[index] = BinaryPrimitives.ReadHalfLittleEndian(source[(index * 2)..]);
        return SqlValue.HalfVector(halves);
    }

    private static byte[] EncodeVariant(VariantSqlValue variant)
    {
        var type = variant.DeclaredType;
        if (type.StorageFamily is SqlStorageFamily.Variant or SqlStorageFamily.Vector)
            throw new ArgumentException("sql_variant cannot contain this type.", nameof(variant));
        var payload = type.StorageFamily switch
        {
            SqlStorageFamily.Text => Utf8.GetBytes(((TextSqlValue)variant.Value).Value),
            SqlStorageFamily.Binary => ((BinarySqlValue)variant.Value).Value.ToArray(),
            _ => EncodeVariantFixed(variant.Value, type)
        };
        var collation = Utf8.GetBytes(type.Collation ?? string.Empty);
        var bytes = new byte[checked(10 + collation.Length + payload.Length)];
        bytes[0] = (byte)type.Name;
        bytes[1] = type.Precision ?? 0;
        bytes[2] = type.Scale ?? 0;
        bytes[3] = type.IsMax ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), type.Length ?? 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)collation.Length));
        collation.CopyTo(bytes.AsSpan(10));
        payload.CopyTo(bytes.AsSpan(10 + collation.Length));
        return bytes;
    }

    private static byte[] EncodeVariantFixed(SqlValue value, SqlType type)
    {
        var column = new ColumnDefinition(new ColumnId(0), "variant", type, false);
        var bytes = new byte[GetFixedWidth(column)];
        WriteFixedValue(bytes, value, column);
        return bytes;
    }

    private static SqlValue DecodeVariant(ReadOnlySpan<byte> source)
    {
        if (source.Length < 10) throw new StorageFormatException("sql_variant header is truncated.");
        var name = (SqlTypeName)source[0];
        if (!Enum.IsDefined(name)) throw new StorageFormatException("Unknown sql_variant type tag.");
        var precision = source[1] == 0 ? null : (byte?)source[1];
        var scale = source[2] == 0 && name is not (SqlTypeName.Decimal or SqlTypeName.Numeric) ? null : (byte?)source[2];
        var isMax = source[3] switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid sql_variant flags.") };
        var length = BinaryPrimitives.ReadInt32LittleEndian(source[4..]);
        var collationLength = BinaryPrimitives.ReadUInt16LittleEndian(source[8..]);
        if (collationLength > source.Length - 10) throw new StorageFormatException("sql_variant collation is truncated.");
        var collation = Utf8.GetString(source.Slice(10, collationLength));
        var type = SqlType.Restore(name, precision, scale, length == 0 ? null : length, isMax,
            collation.Length == 0 ? null : collation, null, null);
        var payload = source[(10 + collationLength)..];
        var value = type.StorageFamily switch
        {
            SqlStorageFamily.Text => SqlValue.Text(Utf8.GetString(payload)),
            SqlStorageFamily.Binary => SqlValue.Binary(payload),
            _ => DecodeFixedValue(payload, new ColumnDefinition(new ColumnId(0), "variant", type, false))
        };
        return SqlValue.Variant(type, value);
    }

    private static SqlValue DecodeDateTimeOffset(ReadOnlySpan<byte> source)
    {
        var utcTicks = BinaryPrimitives.ReadInt64LittleEndian(source);
        var offset = TimeSpan.FromMinutes(BinaryPrimitives.ReadInt16LittleEndian(source[8..]));
        var localTicks = checked(utcTicks + offset.Ticks);
        return SqlValue.DateTimeOffset(new System.DateTimeOffset(localTicks, offset));
    }

    internal static uint CalculateSchemaHash(TableDefinition table)
    {
        var hash = 2166136261u;
        foreach (var column in table.Columns)
        {
            var id = column.Id.Value;
            for (var shift = 0; shift < 64; shift += 8) { hash ^= (byte)(id >> shift); hash *= 16777619u; }
            hash ^= (byte)column.Type.Name; hash *= 16777619u;
            hash ^= column.Type.Precision ?? 0; hash *= 16777619u;
            hash ^= column.Type.Scale ?? 0; hash *= 16777619u;
            var length = column.Type.Length ?? (column.Type.IsMax ? -1 : 0);
            for (var shift = 0; shift < 32; shift += 8) { hash ^= (byte)(length >> shift); hash *= 16777619u; }
            var dimensions = column.Type.VectorDimensions ?? 0;
            hash ^= (byte)dimensions; hash *= 16777619u;
            hash ^= (byte)(dimensions >> 8); hash *= 16777619u;
            hash ^= (byte)(column.Type.VectorBaseType ?? 0); hash *= 16777619u;
            foreach (var value in Utf8.GetBytes(column.Type.Collation ?? string.Empty)) { hash ^= value; hash *= 16777619u; }
            hash ^= (byte)(column.Type.XmlContentKind ?? 0); hash *= 16777619u;
            AddString(ref hash, column.Type.XmlSchemaCollection?.SchemaName);
            AddString(ref hash, column.Type.XmlSchemaCollection?.Name);
            hash ^= (byte)column.Type.UserTypeKind; hash *= 16777619u;
            AddString(ref hash, column.Type.UserTypeSchema);
            AddString(ref hash, column.Type.UserTypeName);
            AddString(ref hash, column.Type.ClrAssemblyName);
            AddString(ref hash, column.Type.ClrClassName);
            AddString(ref hash, column.Type.ClrValidationMethodName);
            hash ^= (byte)(column.Type.ClrSerializationFormat ?? 0); hash *= 16777619u;
            var clrSize = column.Type.ClrMaxByteSize ?? 0;
            for (var shift = 0; shift < 32; shift += 8) { hash ^= (byte)(clrSize >> shift); hash *= 16777619u; }
            hash ^= column.Type.ClrIsByteOrdered ? (byte)1 : (byte)0; hash *= 16777619u;
            hash ^= column.Type.ClrIsFixedLength ? (byte)1 : (byte)0; hash *= 16777619u;
            hash ^= column.Type.UserTypeNullable == true ? (byte)1 : (byte)0; hash *= 16777619u;
            if (column.Type.BaseType is { } baseType) AddString(ref hash, GetTypeIdentity(baseType));
            hash ^= column.IsNullable ? (byte)1 : (byte)0; hash *= 16777619u;
            hash ^= column.IsSparse ? (byte)1 : (byte)0; hash *= 16777619u;
            hash ^= column.IsColumnSet ? (byte)1 : (byte)0; hash *= 16777619u;
            AddString(ref hash, column.Encryption?.KeyName);
            hash ^= (byte)(column.Encryption?.EncryptionType ?? 0); hash *= 16777619u;
            AddString(ref hash, column.Encryption?.Algorithm);
        }
        return hash;

        static void AddString(ref uint current, string? value)
        {
            foreach (var item in Utf8.GetBytes(value ?? string.Empty)) { current ^= item; current *= 16777619u; }
            current ^= 0xff; current *= 16777619u;
        }

        static string GetTypeIdentity(SqlType type) => string.Join('|', type.Name, type.Precision, type.Scale,
            type.Length, type.IsMax, type.Collation, type.VectorDimensions, type.VectorBaseType,
            type.XmlContentKind, type.XmlSchemaCollection?.SchemaName, type.XmlSchemaCollection?.Name,
            type.UserTypeKind, type.UserTypeSchema, type.UserTypeName,
            type.UserTypeNullable, type.ClrAssemblyName, type.ClrClassName, type.ClrSerializationFormat,
            type.ClrMaxByteSize, type.ClrIsByteOrdered, type.ClrIsFixedLength, type.ClrValidationMethodName,
            type.BaseType is null ? string.Empty : GetTypeIdentity(type.BaseType));
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

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Orchestrations.RowPayloads;

namespace sql_storage_engine.Services.Coordinations.RowFormats;
internal sealed partial class RowFormatContextCoordinationService : IRowFormatContextCoordinationService
{
    private readonly RowScalarPayloadOrchestrationService scalar;
    private readonly RowTextPayloadOrchestrationService text;
    private readonly RowSpatialPayloadOrchestrationService spatial;
    private const ushort FormatVersion = RowFormat.FormatVersion;
    private const int HeaderLength = RowFormat.HeaderLength;
    private const int MaximumEncodedLength = RowFormat.MaximumEncodedLength;
    private const int MaximumInlineValueLength = RowFormat.MaximumInlineValueLength;
    private const int VariableEntryLength = RowFormat.VariableEntryLength;
    public RowFormatContextCoordinationService(RowScalarPayloadOrchestrationService scalar, RowTextPayloadOrchestrationService text, RowSpatialPayloadOrchestrationService spatial)
    {
        this.scalar = scalar;
        this.text = text;
        this.spatial = spatial;
    }

    public RowEncodingContext ProcessRowEncodingContextSpatialEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSpatial(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.Bytes = spatial.WriteRowPayloadContextSpatial(rowPayloadContext: new RowPayloadContext { Value = rowEncodingContext.Value })
            .Bytes;

        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextSpatialBounds(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSpatial(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.Bounds = spatial.ReadRowPayloadContextBounds(rowPayloadContext: new RowPayloadContext { Value = rowEncodingContext.Value })
            .Bounds;

        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateRow(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Bytes = EncodeCore(row: rowEncodingContext.Row!, table: rowEncodingContext.Table!, overflowReferences: rowEncodingContext.OverflowReferences);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextDecode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Row = Decode(source: rowEncodingContext.Source.Span, table: rowEncodingContext.Table!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextDecodeSelected(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSelected(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Selected = DecodeSelected(source: rowEncodingContext.Source.Span, table: rowEncodingContext.Table!, columnIds: rowEncodingContext.ColumnIds!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextHeader(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Header = ReadAndValidateHeader(source: rowEncodingContext.Source.Span, table: rowEncodingContext.Table!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextVariables(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.VariableEntries = ReadVariableEntries(source: rowEncodingContext.Source.Span, table: rowEncodingContext.Table!, header: rowEncodingContext.Header);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextWidth(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumn(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Width = GetFixedWidth(column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextIsVariable(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumn(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.IsVariable = IsVariable(column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextVariableEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Bytes = EncodeVariableValue(value: rowEncodingContext.Value!, column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextLogicalEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Bytes = EncodeLogicalValue(value: rowEncodingContext.Value!, column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextVariableDecode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSource(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Value = DecodeVariableValue(source: rowEncodingContext.Source.Span, column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextFixedDecode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSource(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Value = DecodeFixedValue(source: rowEncodingContext.Source.Span, column: rowEncodingContext.Column!);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextSchemaHash(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateTable(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.SchemaHash = CalculateSchemaHash(table: rowEncodingContext.Table!);
        return rowEncodingContext;
    });

    private byte[] WriteUtf8(string value) =>
        text.WriteRowPayloadContextUtf8(rowPayloadContext: new RowPayloadContext { Value = new TextSqlValue { Value = value, StorageFamily = SqlStorageFamily.Text } })
            .Bytes!;

    private string ReadUtf8(ReadOnlySpan<byte> value) =>
        ((TextSqlValue)text.ReadRowPayloadContextUtf8(rowPayloadContext: new RowPayloadContext { Source = value.ToArray() })
            .Value!).Value;

    private int GetFixedWidth(ColumnDefinition column) =>
        scalar.ReadRowPayloadContextWidth(rowPayloadContext: new RowPayloadContext { Column = column })
            .Width;

    private void WriteFixedValue(Span<byte> destination, SqlValue value, ColumnDefinition column) =>
        scalar.WriteRowPayloadContextFixed(rowPayloadContext: new RowPayloadContext { Column = column, Value = value })
            .Bytes!.CopyTo(destination: destination);

    private SqlValue DecodeFixedValue(ReadOnlySpan<byte> source, ColumnDefinition column) =>
        scalar.ReadRowPayloadContextFixed(rowPayloadContext: new RowPayloadContext { Column = column, Source = source.ToArray() })
            .Value!;

    private byte[] EncodeVariableValue(SqlValue value, ColumnDefinition column)
    {
        var plaintext = EncodeLogicalValue(value: value, column: column);

        return column.Encryption is null ? plaintext : scalar.EncryptRowPayloadContext(rowPayloadContext: new RowPayloadContext { Column = column, Source = plaintext })
            .Bytes!;
    }

    private byte[] EncodeLogicalValue(SqlValue value, ColumnDefinition column)
    {
        if (!IsVariable(type: column.Type))
        {
            return scalar.WriteRowPayloadContextFixed(rowPayloadContext: new RowPayloadContext { Column = column with { Encryption = null, IsSparse = false, IsColumnSet = false }, Value = value })
            .Bytes!;
        }

        return value switch
        {
            TextSqlValue => text.WriteRowPayloadContextText(rowPayloadContext: new RowPayloadContext { Column = column, Value = value })
            .Bytes!,
            SpatialSqlValue => spatial.WriteRowPayloadContextSpatial(rowPayloadContext: new RowPayloadContext { Column = column, Value = value })
            .Bytes!,
            VariantSqlValue variant => EncodeVariant(variant: variant),
            _ => scalar.WriteRowPayloadContextScalar(rowPayloadContext: new RowPayloadContext { Column = column, Value = value })
            .Bytes!
        };
    }

    private SqlValue DecodeVariableValue(ReadOnlySpan<byte> source, ColumnDefinition column)
    {
        try
        {
            var plaintext = column.Encryption is null ? source.ToArray() : scalar.DecryptRowPayloadContext(rowPayloadContext: new RowPayloadContext { Column = column, Source = source.ToArray() })
            .Bytes!;

            if (!IsVariable(type: column.Type))
            {
                return DecodeFixedValue(source: plaintext, column: column with { Encryption = null, IsSparse = false, IsColumnSet = false });
            }

            return column.Type.Name switch
            {
                SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Xml or SqlTypeName.Json => text.ReadRowPayloadContextText(rowPayloadContext: new RowPayloadContext { Column = column, Source = plaintext })
            .Value!,
                SqlTypeName.Geometry or SqlTypeName.Geography => spatial.ReadRowPayloadContextSpatial(rowPayloadContext: new RowPayloadContext { Column = column, Source = plaintext })
            .Value!,
                SqlTypeName.SqlVariant => DecodeVariant(source: plaintext),
                _ => scalar.ReadRowPayloadContextScalar(rowPayloadContext: new RowPayloadContext { Column = column, Source = plaintext })
            .Value!
            };
        }
        catch (StorageFormatException)
        {
            throw;
        }
        catch (Exception exception)when (exception is ArgumentException or DecoderFallbackException or OverflowException)
        {
            throw new StorageFormatException(message: $"Column '{column.Name}' contains an invalid {column.Type.Name} value.", innerException: exception);
        }
    }

    private void CheckOverflowReference(OverflowReference overflowReference)
    {
        if (overflowReference.FirstPageId.Value == 0 || overflowReference.TotalLength <= 0 || overflowReference.TotalLength > OverflowReferenceFormat.MaximumValueLength)
        {
            throw new StorageFormatException(message: "Invalid overflow reference in a row.");
        }
    }

    private void WriteOverflowReference(Span<byte> destination, OverflowReference overflowReference)
    {
        CheckOverflowReference(overflowReference: overflowReference);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: destination, value: overflowReference.FirstPageId.Value);
        BinaryPrimitives.WriteInt64LittleEndian(destination: destination[8..], value: overflowReference.TotalLength);
    }

    private static SqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        new BinarySqlValue
    {
        Content = sql_storage_engine.Brokers.BinaryPayloads.BinaryPayloadBroker.ReadBinaryContent(bytes: value),
        StorageFamily = SqlStorageFamily.Binary
    };

    private static ReadOnlyMemory<byte> ReadBinarySqlValue(BinarySqlValue binarySqlValue) =>
        binarySqlValue.Content.Select(selector: item => checked((byte)item))
            .ToArray();

    private byte[] EncodeCore(Row row, TableDefinition table, IReadOnlyDictionary<int, OverflowReference>? overflowReferences)
    {
        ArgumentNullException.ThrowIfNull(argument: row);
        ArgumentNullException.ThrowIfNull(argument: table);

        if (table.Columns.Count > ushort.MaxValue)
        {
            throw new ArgumentException("Schema has too many columns.", nameof(table));
        }

        var nullBytes = checked((table.Columns.Count + 7) / 8);
        var fixedLength = table.Columns.Sum(selector: GetFixedWidth);

        var variableColumns = table.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => IsVariable(column: item.column) && (!IsOmittable(column: item.column) || !row.Values[item.index].IsNull))
            .ToArray();

        var variableValues = new (RowValueStorage Storage, byte[] Bytes)[variableColumns.Length];
        var variableLength = 0;

        for (var variableIndex = 0; variableIndex < variableColumns.Length; variableIndex++)
        {
            var value = row.Values[variableColumns[variableIndex].index];
            var columnIndex = variableColumns[variableIndex].index;
            RowValueStorage storage;
            byte[] encodedValue;

            if (overflowReferences is not null && overflowReferences.TryGetValue(key: columnIndex, value: out var reference))
            {
                CheckOverflowReference(overflowReference: reference);
                storage = RowValueStorage.Overflow;
                encodedValue = new byte[OverflowReferenceFormat.EncodedLength];
                WriteOverflowReference(destination: encodedValue, overflowReference: reference);
            }
            else
            {
                storage = value.IsNull ? RowValueStorage.Null : RowValueStorage.Inline;
                encodedValue = value.IsNull ? [] : EncodeVariableValue(value: value, column: variableColumns[variableIndex].column);
            }

            if (storage == RowValueStorage.Inline && encodedValue.Length > MaximumInlineValueLength)
            {
                throw new ArgumentException($"Inline value exceeds {MaximumInlineValueLength} bytes.", nameof(row));
            }

            variableValues[variableIndex] = (storage, encodedValue);
            variableLength = checked(variableLength + encodedValue.Length);
        }

        var variableTableOffset = checked(HeaderLength + nullBytes + fixedLength);
        var variableDataOffset = checked(variableTableOffset + variableColumns.Length * VariableEntryLength);
        var totalLength = checked(variableDataOffset + variableLength);

        if (totalLength > MaximumEncodedLength)
        {
            throw new ArgumentException("Encoded row exceeds the maximum size.", nameof(row));
        }

        var bytes = new byte[totalLength];
        WriteHeader(destination: bytes, table: table, nullBytes: checked((ushort)nullBytes), variableCount: checked((ushort)variableColumns.Length), fixedLength: checked((uint)fixedLength), variableTableOffset: checked((uint)variableTableOffset), variableDataOffset: checked((uint)variableDataOffset), totalLength: checked((uint)totalLength));
        var fixedOffset = HeaderLength + nullBytes;

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var value = row.Values[index];

            if (value.IsNull)
            {
                bytes[HeaderLength + index / 8] |= checked((byte)(1 << (index % 8)));
            }
            else if (!IsVariable(column: column))
            {
                WriteFixedValue(destination: bytes.AsSpan(start: fixedOffset), value: value, column: column);
            }

            fixedOffset += GetFixedWidth(column: column);
        }

        var cursor = variableDataOffset;

        for (var variableIndex = 0; variableIndex < variableColumns.Length; variableIndex++)
        {
            var entry = bytes.AsSpan(start: variableTableOffset + variableIndex * VariableEntryLength, length: VariableEntryLength);
            BinaryPrimitives.WriteUInt16LittleEndian(destination: entry, value: checked((ushort)variableColumns[variableIndex].index));
            BinaryPrimitives.WriteUInt16LittleEndian(destination: entry[2..], value: (ushort)variableValues[variableIndex].Storage);
            BinaryPrimitives.WriteUInt32LittleEndian(destination: entry[4..], value: checked((uint)cursor));
            BinaryPrimitives.WriteUInt32LittleEndian(destination: entry[8..], value: checked((uint)variableValues[variableIndex].Bytes.Length));
            variableValues[variableIndex].Bytes.CopyTo(destination: bytes.AsSpan(start: cursor));
            cursor = checked(cursor + variableValues[variableIndex].Bytes.Length);
        }

        return bytes;
    }

    private Row Decode(ReadOnlySpan<byte> source, TableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(argument: table);
        var header = ReadAndValidateHeader(source: source, table: table);
        var variables = ReadVariableEntries(source: source, table: table, header: header);
        var values = new SqlValue[table.Columns.Count];
        var fixedOffset = checked(HeaderLength + header.NullBitmapLength);

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            values[index] = DecodeColumn(source: source, column: column, columnIndex: index, fixedOffset: fixedOffset, variables: variables);

            fixedOffset = checked(fixedOffset + GetFixedWidth(column: column));
        }

        return new Row
        {
            Values = values
        };
    }

    private IReadOnlyDictionary<ColumnId, SqlValue> DecodeSelected(ReadOnlySpan<byte> source, TableDefinition table, IEnumerable<ColumnId> columnIds)
    {
        ArgumentNullException.ThrowIfNull(argument: columnIds);
        var requested = columnIds.ToArray();

        var requestedSet = requested.ToHashSet();

        if (requestedSet.Count != requested.Length)
        {
            throw new ArgumentException("Requested columns must be unique.", nameof(columnIds));
        }

        if (requestedSet.Any(predicate: id => table.Columns.All(predicate: column => column.Id != id)))
        {
            throw new ArgumentException("Requested column is not present in the schema.", nameof(columnIds));
        }

        var header = ReadAndValidateHeader(source: source, table: table);
        var variables = ReadVariableEntries(source: source, table: table, header: header);
        Dictionary<ColumnId, SqlValue> result = [];
        var fixedOffset = HeaderLength + header.NullBitmapLength;

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];

            if (requestedSet.Contains(item: column.Id))
            {
                result.Add(key: column.Id, value: DecodeColumn(source: source, column: column, columnIndex: index, fixedOffset: fixedOffset, variables: variables));
            }

            fixedOffset = checked(fixedOffset + GetFixedWidth(column: column));
        }

        return result;
    }

    private RowHeader ReadAndValidateHeader(ReadOnlySpan<byte> source, TableDefinition table)
    {
        if (source.Length < HeaderLength)
        {
            throw new StorageFormatException("Row header is truncated.");
        }

        var header = new RowHeader
        {
            Version = BinaryPrimitives.ReadUInt16LittleEndian(source: source),
            ColumnCount = BinaryPrimitives.ReadUInt16LittleEndian(source: source[2..]),
            NullBitmapLength = BinaryPrimitives.ReadUInt16LittleEndian(source: source[4..]),
            VariableCount = BinaryPrimitives.ReadUInt16LittleEndian(source: source[6..]),
            FixedDataLength = BinaryPrimitives.ReadUInt32LittleEndian(source: source[8..]),
            VariableTableOffset = BinaryPrimitives.ReadUInt32LittleEndian(source: source[12..]),
            VariableDataOffset = BinaryPrimitives.ReadUInt32LittleEndian(source: source[16..]),
            TotalLength = BinaryPrimitives.ReadUInt32LittleEndian(source: source[20..]),
            SchemaHash = BinaryPrimitives.ReadUInt32LittleEndian(source: source[24..])
        };

        if (BinaryPrimitives.ReadUInt32LittleEndian(source: source[28..]) != 0)
        {
            throw new StorageFormatException("Reserved row-header bytes must be zero.");
        }

        if (header.Version != FormatVersion)
        {
            throw new StorageFormatException($"Unsupported row format version {header.Version}.");
        }

        if (header.ColumnCount != table.Columns.Count)
        {
            throw new StorageFormatException("Encoded row column count does not match schema.");
        }

        if (header.SchemaHash != CalculateSchemaHash(table: table))
        {
            throw new StorageFormatException("Encoded row schema fingerprint does not match.");
        }

        var expectedNullBytes = checked((table.Columns.Count + 7) / 8);

        if (header.NullBitmapLength != expectedNullBytes)
        {
            throw new StorageFormatException("Invalid row null-bitmap length.");
        }

        if (header.TotalLength > MaximumEncodedLength || header.TotalLength != source.Length)
        {
            throw new StorageFormatException("Encoded row total length is invalid or truncated.");
        }

        var fixedStart = checked((uint)(HeaderLength + expectedNullBytes));
        var fixedEnd = checked(fixedStart + header.FixedDataLength);
        var minimumVariableCount = table.Columns.Count(predicate: column => IsVariable(column: column) && !IsOmittable(column: column));
        var maximumVariableCount = table.Columns.Count(predicate: IsVariable);

        if (header.VariableCount < minimumVariableCount || header.VariableCount > maximumVariableCount)
        {
            throw new StorageFormatException("Variable-column count does not match schema.");
        }

        var expectedVariableDataOffset = checked(fixedEnd + (uint)(header.VariableCount * VariableEntryLength));

        if (header.VariableTableOffset != fixedEnd || header.VariableDataOffset != expectedVariableDataOffset || header.VariableDataOffset > header.TotalLength)
        {
            throw new StorageFormatException("Encoded row region offsets are invalid.");
        }

        var expectedFixedLength = table.Columns.Sum(selector: GetFixedWidth);

        if (header.FixedDataLength != expectedFixedLength)
        {
            throw new StorageFormatException("Fixed row region does not match schema.");
        }

        var unusedNullBits = expectedNullBytes * 8 - table.Columns.Count;

        if (unusedNullBits > 0)
        {
            var validMask = (1 << (8 - unusedNullBits)) - 1;

            if ((source[HeaderLength + expectedNullBytes - 1] & ~validMask) != 0)
            {
                throw new StorageFormatException("Unused null-bitmap bits must be zero.");
            }
        }

        return header;
    }

    private Dictionary<int, RowVariableEntry> ReadVariableEntries(ReadOnlySpan<byte> source, TableDefinition table, RowHeader header)
    {
        Dictionary<int, RowVariableEntry> entries = [];

        var cursor = checked((int)header.VariableDataOffset);
        var previousColumnIndex = -1;

        for (var variableIndex = 0; variableIndex < header.VariableCount; variableIndex++)
        {
            var entryOffset = checked((int)header.VariableTableOffset + variableIndex * VariableEntryLength);
            var entry = source.Slice(start: entryOffset, length: VariableEntryLength);
            var columnIndex = BinaryPrimitives.ReadUInt16LittleEndian(source: entry);
            var storage = (RowValueStorage)BinaryPrimitives.ReadUInt16LittleEndian(source: entry[2..]);

            if (!Enum.IsDefined(value: storage))
            {
                throw new StorageFormatException("Unknown variable-value storage tag.");
            }

            if (columnIndex >= table.Columns.Count || !IsVariable(column: table.Columns[columnIndex]) || columnIndex <= previousColumnIndex)
            {
                throw new StorageFormatException("Variable offset table does not follow schema order.");
            }

            previousColumnIndex = columnIndex;
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(source: entry[4..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(source: entry[8..]);
            var isNull = (source[HeaderLength + columnIndex / 8] & (1 << (columnIndex % 8))) != 0;
            var column = table.Columns[columnIndex];

            if (isNull != (storage == RowValueStorage.Null) || IsOmittable(column: column) && isNull || storage == RowValueStorage.Null && length != 0 || storage == RowValueStorage.Overflow && length != OverflowReferenceFormat.EncodedLength || storage == RowValueStorage.Inline && length > MaximumInlineValueLength || offset != cursor || checked((ulong)offset + length) > header.TotalLength)
            {
                throw new StorageFormatException("Variable value offsets overlap, decrease, or exceed the row.");
            }

            entries.Add(key: columnIndex, value: new RowVariableEntry { Storage = storage, Offset = checked((int)offset), Length = checked((int)length) });
            cursor = checked((int)(offset + length));
        }

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];

            if (!IsVariable(column: column))
            {
                continue;
            }

            var isNull = (source[HeaderLength + index / 8] & (1 << (index % 8))) != 0;
            var isOmittable = IsOmittable(column: column);
            var hasEntry = entries.ContainsKey(key: index);

            if ((!isOmittable || !isNull) && !hasEntry)
            {
                throw new StorageFormatException("Variable offset table omits a required value.");
            }

            if (isOmittable && isNull && hasEntry)
            {
                throw new StorageFormatException("A NULL sparse or virtual column must not have a variable entry.");
            }
        }

        if (cursor != header.TotalLength)
        {
            throw new StorageFormatException("Variable values do not cover the declared data region.");
        }

        return entries;
    }

    private SqlValue DecodeColumn(ReadOnlySpan<byte> source, ColumnDefinition column, int columnIndex, int fixedOffset, IReadOnlyDictionary<int, RowVariableEntry> variables)
    {
        var isNull = (source[HeaderLength + columnIndex / 8] & (1 << (columnIndex % 8))) != 0;

        if (isNull)
        {
            if (!column.IsNullable)
            {
                throw new StorageFormatException($"Non-nullable column '{column.Name}' is encoded as NULL.");
            }

            return new NullSqlValue
            {
                IsNull = true
            };
        }

        SqlValue value;

        if (!IsVariable(column: column))
        {
            value = DecodeFixedValue(source: source[fixedOffset..], column: column);
        }
        else
        {
            var entry = variables[columnIndex];

            if (entry.Storage != RowValueStorage.Inline)
            {
                throw new StorageFormatException("Overflow row values require the overflow-aware decoder.");
            }

            value = DecodeVariableValue(source: source.Slice(start: entry.Offset, length: entry.Length), column: column);
        }

        return value;
    }

    private bool IsVariable(SqlType type) =>
        type.Name switch
    {
        SqlTypeName.Bit or SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt or SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money or SqlTypeName.Real or SqlTypeName.Float or SqlTypeName.Date or SqlTypeName.Time or SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 or SqlTypeName.DateTimeOffset or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.UniqueIdentifier => false,
        _ => true
    };

    private bool IsVariable(ColumnDefinition column) =>
        column.IsSparse || column.IsColumnSet || column.Encryption is not null || IsVariable(type: column.Type);

    private bool IsOmittable(ColumnDefinition column) =>
        column.IsSparse || column.IsColumnSet;

    private uint CalculateSchemaHash(TableDefinition table)
    {
        var hash = 2166136261u;

        foreach (var column in table.Columns)
        {
            var id = column.Id.Value;

            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(id >> shift);
                hash *= 16777619u;
            }

            hash ^= (byte)column.Type.Name;
            hash *= 16777619u;
            hash ^= column.Type.Precision ?? 0;
            hash *= 16777619u;
            hash ^= column.Type.Scale ?? 0;
            hash *= 16777619u;
            var length = column.Type.Length ?? (column.Type.IsMax ? -1 : 0);

            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(length >> shift);
                hash *= 16777619u;
            }

            var dimensions = column.Type.VectorDimensions ?? 0;
            hash ^= (byte)dimensions;
            hash *= 16777619u;
            hash ^= (byte)(dimensions >> 8);
            hash *= 16777619u;
            hash ^= (byte)(column.Type.VectorBaseType ?? 0);
            hash *= 16777619u;

            foreach (var value in WriteUtf8(value: column.Type.Collation ?? string.Empty))
            {
                hash ^= value;
                hash *= 16777619u;
            }

            hash ^= (byte)(column.Type.XmlContentKind ?? 0);
            hash *= 16777619u;
            AddString(current: ref hash, value: column.Type.XmlSchemaCollection?.SchemaName);
            AddString(current: ref hash, value: column.Type.XmlSchemaCollection?.Name);
            hash ^= (byte)column.Type.UserTypeKind;
            hash *= 16777619u;
            AddString(current: ref hash, value: column.Type.UserTypeSchema);
            AddString(current: ref hash, value: column.Type.UserTypeName);
            AddString(current: ref hash, value: column.Type.ClrAssemblyName);
            AddString(current: ref hash, value: column.Type.ClrClassName);
            AddString(current: ref hash, value: column.Type.ClrValidationMethodName);
            hash ^= (byte)(column.Type.ClrSerializationFormat ?? 0);
            hash *= 16777619u;
            var clrSize = column.Type.ClrMaxByteSize ?? 0;

            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(clrSize >> shift);
                hash *= 16777619u;
            }

            hash ^= column.Type.ClrIsByteOrdered ? (byte)1 : (byte)0;
            hash *= 16777619u;
            hash ^= column.Type.ClrIsFixedLength ? (byte)1 : (byte)0;
            hash *= 16777619u;
            hash ^= column.Type.UserTypeNullable == true ? (byte)1 : (byte)0;
            hash *= 16777619u;

            if (column.Type.BaseType is { } baseType)
            {
                AddString(current: ref hash, value: GetTypeIdentity(type: baseType));
            }

            hash ^= column.IsNullable ? (byte)1 : (byte)0;
            hash *= 16777619u;
            hash ^= column.IsSparse ? (byte)1 : (byte)0;
            hash *= 16777619u;
            hash ^= column.IsColumnSet ? (byte)1 : (byte)0;
            hash *= 16777619u;
            AddString(current: ref hash, value: column.Encryption?.KeyName);
            hash ^= (byte)(column.Encryption?.EncryptionType ?? 0);
            hash *= 16777619u;
            AddString(current: ref hash, value: column.Encryption?.Algorithm);
        }

        return hash;

        void AddString(ref uint current, string? value)
        {
            foreach (var item in WriteUtf8(value: value ?? string.Empty))
            {
                current ^= item;
                current *= 16777619u;
            }

            current ^= 0xff;
            current *= 16777619u;
        }

        static string GetTypeIdentity(SqlType type) => string.Join(separator: '|', values: [type.Name, type.Precision, type.Scale, type.Length, type.IsMax, type.Collation, type.VectorDimensions, type.VectorBaseType, type.XmlContentKind, type.XmlSchemaCollection?.SchemaName, type.XmlSchemaCollection?.Name, type.UserTypeKind, type.UserTypeSchema, type.UserTypeName, type.UserTypeNullable, type.ClrAssemblyName, type.ClrClassName, type.ClrSerializationFormat, type.ClrMaxByteSize, type.ClrIsByteOrdered, type.ClrIsFixedLength, type.ClrValidationMethodName, type.BaseType is null ? string.Empty : GetTypeIdentity(type: type.BaseType)]);
    }

    private void WriteHeader(Span<byte> destination, TableDefinition table, ushort nullBytes, ushort variableCount, uint fixedLength, uint variableTableOffset, uint variableDataOffset, uint totalLength)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination: destination, value: FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: destination[2..], value: checked((ushort)table.Columns.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(destination: destination[4..], value: nullBytes);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: destination[6..], value: variableCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[8..], value: fixedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[12..], value: variableTableOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[16..], value: variableDataOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[20..], value: totalLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[24..], value: CalculateSchemaHash(table: table));
    }

    private byte[] EncodeVariant(VariantSqlValue variant)
    {
        var type = variant.DeclaredType;

        if (ReadStorageFamily(type: type)is SqlStorageFamily.Variant or SqlStorageFamily.Vector)
        {
            throw new ArgumentException("sql_variant cannot contain this type.", nameof(variant));
        }

        var payload = ReadStorageFamily(type: type) switch
        {
            SqlStorageFamily.Text => WriteUtf8(value: ((TextSqlValue)variant.Value).Value),
            SqlStorageFamily.Binary => ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)variant.Value))
            .ToArray(),
            _ => EncodeVariantFixed(value: variant.Value, type: type)};

        var collation = WriteUtf8(value: type.Collation ?? string.Empty);
        var bytes = new byte[checked(10 + collation.Length + payload.Length)];
        bytes[0] = (byte)type.Name;
        bytes[1] = type.Precision ?? 0;
        bytes[2] = type.Scale ?? 0;
        bytes[3] = type.IsMax ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(destination: bytes.AsSpan(start: 4), value: type.Length ?? 0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8), value: checked((ushort)collation.Length));
        collation.CopyTo(destination: bytes.AsSpan(start: 10));
        payload.CopyTo(destination: bytes.AsSpan(start: 10 + collation.Length));
        return bytes;
    }

    private byte[] EncodeVariantFixed(SqlValue value, SqlType type)
    {
        var column = new ColumnDefinition
        {
            Id = new sql_storage_engine.Models.Identifiers.ColumnId
            {
                Value = 0
            },
            Name = "variant",
            Type = type,
            IsNullable = false
        };

        var bytes = new byte[GetFixedWidth(column: column)];
        WriteFixedValue(destination: bytes, value: value, column: column);
        return bytes;
    }

    private SqlValue DecodeVariant(ReadOnlySpan<byte> source)
    {
        if (source.Length < 10)
        {
            throw new StorageFormatException("sql_variant header is truncated.");
        }

        var name = (SqlTypeName)source[0];

        if (!Enum.IsDefined(value: name))
        {
            throw new StorageFormatException("Unknown sql_variant type tag.");
        }

        var precision = source[1] == 0 ? null : (byte? )source[1];
        var scale = source[2] == 0 && name is not (SqlTypeName.Decimal or SqlTypeName.Numeric) ? null : (byte? )source[2];

        var isMax = source[3] switch
        {
            0 => false,
            1 => true,
            _ => throw new StorageFormatException("Invalid sql_variant flags.")};

        var length = BinaryPrimitives.ReadInt32LittleEndian(source: source[4..]);
        var collationLength = BinaryPrimitives.ReadUInt16LittleEndian(source: source[8..]);

        if (collationLength > source.Length - 10)
        {
            throw new StorageFormatException("sql_variant collation is truncated.");
        }

        var collation = ReadUtf8(value: source.Slice(start: 10, length: collationLength));

        var type = new SqlType
        {
            Name = name,
            Precision = precision,
            Scale = scale,
            Length = length == 0 ? null : length,
            IsMax = isMax,
            Collation = collation.Length == 0 ? null : collation,
            VectorDimensions = null,
            VectorBaseType = null
        };

        var payload = source[(10 + collationLength)..];

        var value = ReadStorageFamily(type: type) switch
        {
            SqlStorageFamily.Text => new TextSqlValue
            {
                Value = ReadUtf8(value: payload),
                StorageFamily = SqlStorageFamily.Text
            },
            SqlStorageFamily.Binary => CreateBinarySqlValue(value: payload),
            _ => DecodeFixedValue(source: payload, column: new ColumnDefinition { Id = new sql_storage_engine.Models.Identifiers.ColumnId { Value = 0 }, Name = "variant", Type = type, IsNullable = false })};

        return new VariantSqlValue
        {
            DeclaredType = type,
            Value = value,
            StorageFamily = SqlStorageFamily.Variant
        };
    }

    private static SqlStorageFamily ReadStorageFamily(SqlType type) =>
        type.Name switch
    {
        SqlTypeName.Bit => SqlStorageFamily.Boolean,
        SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt => SqlStorageFamily.Integer,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney => SqlStorageFamily.Decimal,
        SqlTypeName.Real or SqlTypeName.Float => SqlStorageFamily.Float,
        SqlTypeName.Date => SqlStorageFamily.Date,
        SqlTypeName.Time => SqlStorageFamily.Time,
        SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => SqlStorageFamily.DateTime,
        SqlTypeName.DateTimeOffset => SqlStorageFamily.DateTimeOffset,
        SqlTypeName.UniqueIdentifier => SqlStorageFamily.UniqueIdentifier,
        SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText or SqlTypeName.Xml or SqlTypeName.Json => SqlStorageFamily.Text,
        SqlTypeName.Vector => SqlStorageFamily.Vector,
        SqlTypeName.SqlVariant => SqlStorageFamily.Variant,
        _ => SqlStorageFamily.Binary
    };

    internal static RowFormatContextCoordinationService CreateRowFormatContextCoordinationService() =>
        new RowFormatContextCoordinationService(scalar: RowScalarPayloadOrchestrationService.CreateRowScalarPayloadOrchestrationService(), text: RowTextPayloadOrchestrationService.CreateRowTextPayloadOrchestrationService(), spatial: RowSpatialPayloadOrchestrationService.CreateRowSpatialPayloadOrchestrationService());

    public RowEncodingContext ReadRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetText(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.ColumnSetElements = text.ReadRowPayloadContextColumnSet(rowPayloadContext: new RowPayloadContext { Text = rowEncodingContext.Text })
            .ColumnSetElements;

        return rowEncodingContext;
    });

    public RowEncodingContext WriteRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetElements(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.Text = text.WriteRowPayloadContextColumnSet(rowPayloadContext: new RowPayloadContext { ColumnSetElements = rowEncodingContext.ColumnSetElements })
            .Text;

        return rowEncodingContext;
    });
}
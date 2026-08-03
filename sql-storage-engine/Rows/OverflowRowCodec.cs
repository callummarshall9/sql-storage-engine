using System.Buffers.Binary;
using System.Text;
using sql_storage_engine.Overflow;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

public sealed record RowEncodingResult(
    byte[] Bytes,
    IReadOnlyList<OverflowReference> NewlyAllocated,
    IReadOnlyList<OverflowReference> Retired);

/// <summary>Selects inline or overflow storage while preserving logical row values.</summary>
public sealed class OverflowRowCodec
{
    private readonly OverflowManager _overflowManager;

    public OverflowRowCodec(OverflowManager overflowManager, int inlineThreshold)
    {
        ArgumentNullException.ThrowIfNull(overflowManager);
        if (inlineThreshold < 0 || inlineThreshold > RowCodec.MaximumInlineValueLength)
            throw new ArgumentOutOfRangeException(nameof(inlineThreshold));
        _overflowManager = overflowManager;
        InlineThreshold = inlineThreshold;
    }

    public int InlineThreshold { get; }

    public async ValueTask<RowEncodingResult> EncodeAsync(Row row, TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        table.ValidateRow(row);
        Dictionary<int, OverflowReference> references = [];
        List<OverflowReference> allocated = [];
        try
        {
            for (var index = 0; index < table.Columns.Count; index++)
            {
                if (table.Columns[index].Type is not (SqlType.Text or SqlType.Binary) || row.Values[index].IsNull) continue;
                var bytes = GetVariableBytes(row.Values[index]);
                if (bytes.Length <= InlineThreshold) continue;
                var reference = await _overflowManager.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                references.Add(index, reference);
                allocated.Add(reference);
            }
            return new RowEncodingResult(RowCodec.EncodeCore(row, table, references), allocated.AsReadOnly(), Array.Empty<OverflowReference>());
        }
        catch
        {
            await CleanupNewAsync(allocated).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<Row> DecodeAsync(ReadOnlyMemory<byte> source, TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        var header = RowCodec.ReadAndValidateHeader(source.Span, table);
        var variables = RowCodec.ReadVariableEntries(source.Span, table, header);
        var values = new SqlValue[table.Columns.Count];
        var fixedOffset = RowCodec.HeaderLength + header.NullBitmapLength;
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var isNull = (source.Span[RowCodec.HeaderLength + index / 8] & (1 << (index % 8))) != 0;
            if (isNull)
            {
                if (!column.IsNullable) throw new StorageFormatException($"Non-nullable column '{column.Name}' is encoded as NULL.");
                values[index] = SqlValue.Null;
            }
            else if (column.Type == SqlType.Boolean)
            {
                values[index] = source.Span[fixedOffset] switch
                {
                    0 => SqlValue.Boolean(false), 1 => SqlValue.Boolean(true),
                    _ => throw new StorageFormatException($"Invalid boolean byte for column '{column.Name}'.")
                };
            }
            else if (column.Type == SqlType.Integer)
            {
                values[index] = SqlValue.Integer(BinaryPrimitives.ReadInt64LittleEndian(source.Span[fixedOffset..]));
            }
            else
            {
                var entry = variables[index];
                ReadOnlyMemory<byte> bytes = entry.Storage switch
                {
                    RowValueStorage.Inline => source.Slice(entry.Offset, entry.Length),
                    RowValueStorage.Overflow => await _overflowManager.ReadAsync(
                        OverflowReferenceCodec.Read(source.Span.Slice(entry.Offset, entry.Length)), cancellationToken).ConfigureAwait(false),
                    _ => throw new StorageFormatException("Non-null variable value has invalid storage.")
                };
                values[index] = column.Type == SqlType.Text
                    ? DecodeText(bytes.Span, column.Name)
                    : SqlValue.Binary(bytes.Span);
            }
            fixedOffset = checked(fixedOffset + RowCodec.GetFixedWidth(column));
        }
        return new Row(values);
    }

    public async ValueTask<RowEncodingResult> ApplyUpdateAsync(ReadOnlyMemory<byte> currentRow,
        RowUpdate update, TableDefinition table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var current = await DecodeAsync(currentRow, table, cancellationToken).ConfigureAwait(false);
        HashSet<int> updatedIndexes = [];
        foreach (var columnUpdate in update.Columns)
        {
            if (columnUpdate.ColumnIndex < 0 || columnUpdate.ColumnIndex >= table.Columns.Count)
                throw new ArgumentOutOfRangeException(nameof(update), $"Unknown column index {columnUpdate.ColumnIndex}.");
            if (!updatedIndexes.Add(columnUpdate.ColumnIndex))
                throw new ArgumentException($"Column index {columnUpdate.ColumnIndex} is updated more than once.", nameof(update));
            table.Columns[columnUpdate.ColumnIndex].Validate(columnUpdate.Value);
        }
        var values = current.Values.ToArray();
        foreach (var columnUpdate in update.Columns) values[columnUpdate.ColumnIndex] = columnUpdate.Value;
        var replacement = new Row(values);
        var oldReferences = ReadReferences(currentRow.Span, table);
        Dictionary<int, OverflowReference> outputReferences = [];
        List<OverflowReference> allocated = [];
        List<OverflowReference> retired = [];
        try
        {
            foreach (var pair in oldReferences)
            {
                if (updatedIndexes.Contains(pair.Key)) retired.Add(pair.Value);
                else outputReferences.Add(pair.Key, pair.Value);
            }
            foreach (var index in updatedIndexes)
            {
                if (table.Columns[index].Type is not (SqlType.Text or SqlType.Binary) || replacement.Values[index].IsNull) continue;
                var bytes = GetVariableBytes(replacement.Values[index]);
                if (bytes.Length <= InlineThreshold) continue;
                var reference = await _overflowManager.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                outputReferences.Add(index, reference);
                allocated.Add(reference);
            }
            var encoded = RowCodec.EncodeCore(replacement, table, outputReferences);
            return new RowEncodingResult(encoded, allocated.AsReadOnly(), retired.AsReadOnly());
        }
        catch
        {
            await CleanupNewAsync(allocated).ConfigureAwait(false);
            throw;
        }
    }

    public RowValueStorage GetStorage(ReadOnlySpan<byte> row, TableDefinition table, ColumnId columnId)
    {
        var index = table.Columns.Select((column, position) => (column, position))
            .Where(item => item.column.Id == columnId).Select(item => item.position).DefaultIfEmpty(-1).Single();
        if (index < 0) throw new ArgumentException("Unknown column ID.", nameof(columnId));
        if (table.Columns[index].Type is not (SqlType.Text or SqlType.Binary)) return RowValueStorage.Inline;
        var header = RowCodec.ReadAndValidateHeader(row, table);
        return RowCodec.ReadVariableEntries(row, table, header)[index].Storage;
    }

    public IReadOnlyDictionary<ColumnId, OverflowReference> GetOverflowReferences(ReadOnlySpan<byte> row,
        TableDefinition table) => ReadReferences(row, table).ToDictionary(pair => table.Columns[pair.Key].Id, pair => pair.Value);

    private static Dictionary<int, OverflowReference> ReadReferences(ReadOnlySpan<byte> row, TableDefinition table)
    {
        var header = RowCodec.ReadAndValidateHeader(row, table);
        var variables = RowCodec.ReadVariableEntries(row, table, header);
        Dictionary<int, OverflowReference> result = [];
        foreach (var pair in variables)
            if (pair.Value.Storage == RowValueStorage.Overflow)
                result.Add(pair.Key, OverflowReferenceCodec.Read(row.Slice(pair.Value.Offset, pair.Value.Length)));
        return result;
    }

    private static byte[] GetVariableBytes(SqlValue value) => value switch
    {
        TextSqlValue text => RowCodec.Utf8.GetBytes(text.Value),
        BinarySqlValue binary => binary.Value.ToArray(),
        _ => throw new ArgumentException("Value is not variable-width.", nameof(value))
    };

    private static SqlValue DecodeText(ReadOnlySpan<byte> bytes, string columnName)
    {
        try { return SqlValue.Text(RowCodec.Utf8.GetString(bytes)); }
        catch (DecoderFallbackException exception) { throw new StorageFormatException($"Column '{columnName}' contains invalid UTF-8.", exception); }
    }

    private async ValueTask CleanupNewAsync(IReadOnlyList<OverflowReference> references)
    {
        for (var index = references.Count - 1; index >= 0; index--)
            await _overflowManager.FreeAsync(references[index]).ConfigureAwait(false);
    }
}

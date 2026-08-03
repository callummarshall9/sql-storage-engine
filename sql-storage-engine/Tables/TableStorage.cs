using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Tables;

/// <summary>Binds catalog index metadata to its persistent tree.</summary>
public sealed record TableIndex(CatalogIndex Definition, PersistentBPlusTree Tree);

/// <summary>Reports a failed logical table mutation and storage roots requiring deferred cleanup.</summary>
public sealed class TableMutationException : StorageException
{
    public TableMutationException(string message, IReadOnlyList<PageId> unreclaimedPageIds, Exception innerException)
        : base(message, innerException) => UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    public IReadOnlyList<PageId> UnreclaimedPageIds { get; }
}

/// <summary>Coordinates logical rows with their heap, overflow values, and all secondary indexes.</summary>
public sealed class TableStorage
{
    private readonly CatalogTable _table;
    private readonly TableDefinition _schema;
    private readonly TableHeap _heap;
    private readonly OverflowRowCodec _rowCodec;
    private readonly OverflowManager _overflow;
    private readonly TableIndex[] _indexes;

    public TableStorage(CatalogTable table, TableHeap heap, OverflowRowCodec rowCodec,
        OverflowManager overflow, IEnumerable<TableIndex> indexes)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(heap);
        ArgumentNullException.ThrowIfNull(rowCodec);
        ArgumentNullException.ThrowIfNull(overflow);
        ArgumentNullException.ThrowIfNull(indexes);
        _indexes = indexes.ToArray();
        if (_indexes.Any(index => index.Definition.TableId != table.Id))
            throw new ArgumentException("Every index must belong to the table.", nameof(indexes));
        _table = table;
        _schema = CatalogService.ToRowTable(table);
        _heap = heap;
        _rowCodec = rowCodec;
        _overflow = overflow;
    }

    public CatalogTable Definition => _table;

    /// <summary>Validates and inserts one logical row into the heap and every published index.</summary>
    public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        _schema.ValidateRow(row); // No allocation or mutation occurs before complete logical validation.
        RowEncodingResult encoded = await _rowCodec.EncodeAsync(row, _schema, cancellationToken).ConfigureAwait(false);
        RowId? rowId = null;
        List<(TableIndex Index, IndexKey Key)> insertedIndexes = [];
        try
        {
            rowId = await _heap.InsertAsync(encoded.Bytes, cancellationToken).ConfigureAwait(false);
            foreach (var index in _indexes)
            {
                var key = CatalogIndexKey.Encode(row, _table, index.Definition);
                await index.Tree.InsertAsync(key, rowId.Value, cancellationToken).ConfigureAwait(false);
                insertedIndexes.Add((index, key));
            }
            return rowId.Value;
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            for (var index = insertedIndexes.Count - 1; index >= 0; index--)
                try { await insertedIndexes[index].Index.Tree.RemoveAsync(insertedIndexes[index].Key, rowId!.Value).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(insertedIndexes[index].Index.Definition.RootPageId); }
            if (rowId is { } insertedRow)
                try { if (!await _heap.DeleteAsync(insertedRow).ConfigureAwait(false)) unreclaimed.Add(insertedRow.PageId); }
                catch (StorageException) { unreclaimed.Add(insertedRow.PageId); }
            foreach (var reference in encoded.NewlyAllocated.Reverse())
                try { await _overflow.FreeAsync(reference).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(reference.FirstPageId); }
            throw new TableMutationException("Table insertion failed and compensating cleanup was attempted.",
                unreclaimed.Distinct().ToArray(), exception);
        }
    }

    /// <summary>Returns the logical row for a live generation-safe row identifier.</summary>
    public async ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId,
        CancellationToken cancellationToken = default)
    {
        var result = await _heap.ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (result.Result != TableHeapLookupResult.Found) return (false, null);
        return (true, await _rowCodec.DecodeAsync(result.Row, _schema, cancellationToken).ConfigureAwait(false));
    }
}

using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Tables;

/// <summary>Binds catalog index metadata to its persistent tree.</summary>
public sealed class TableIndex(CatalogIndex definition, PersistentBPlusTree tree)
{
    public CatalogIndex Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));
    public PersistentBPlusTree Tree { get; } = tree ?? throw new ArgumentNullException(nameof(tree));
    public int AddCount { get; private set; }
    public int RemoveCount { get; private set; }
    internal async ValueTask AddAsync(IndexKey key, RowId rowId, CancellationToken token)
    { await Tree.InsertAsync(key, rowId, token).ConfigureAwait(false); AddCount++; }
    internal async ValueTask<bool> RemoveAsync(IndexKey key, RowId rowId, CancellationToken token = default)
    { var removed = await Tree.RemoveAsync(key, rowId, token).ConfigureAwait(false); if (removed) RemoveCount++; return removed; }
}

/// <summary>Describes a successful logical row update and whether its physical identifier changed.</summary>
public sealed record TableUpdateResult(bool Updated, RowId PreviousRowId, RowId CurrentRowId)
{
    public bool Relocated => PreviousRowId != CurrentRowId;
}

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
                await index.AddAsync(key, rowId.Value, cancellationToken).ConfigureAwait(false);
                insertedIndexes.Add((index, key));
            }
            return rowId.Value;
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            for (var index = insertedIndexes.Count - 1; index >= 0; index--)
                try { await insertedIndexes[index].Index.RemoveAsync(insertedIndexes[index].Key, rowId!.Value).ConfigureAwait(false); }
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

    /// <summary>Applies selected columns while maintaining changed keys and all RowId references after relocation.</summary>
    public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var current = await _heap.ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (current.Result != TableHeapLookupResult.Found) return new TableUpdateResult(false, rowId, rowId);
        var oldBytes = current.Row.ToArray();
        var oldRow = await _rowCodec.DecodeAsync(current.Row, _schema, cancellationToken).ConfigureAwait(false);
        var replacement = await _rowCodec.ApplyUpdateAsync(current.Row, update, _schema, cancellationToken).ConfigureAwait(false);
        var newRow = await _rowCodec.DecodeAsync(replacement.Bytes, _schema, cancellationToken).ConfigureAwait(false);
        var heapResult = await _heap.UpdateAsync(rowId, replacement.Bytes, cancellationToken).ConfigureAwait(false);
        RowId newRowId = rowId;
        var relocated = heapResult == HeapUpdateResult.RelocationRequired;
        if (relocated) newRowId = await _heap.InsertAsync(replacement.Bytes, cancellationToken).ConfigureAwait(false);
        else if (heapResult != HeapUpdateResult.Updated)
        {
            foreach (var reference in replacement.NewlyAllocated) await _overflow.FreeAsync(reference).ConfigureAwait(false);
            return new TableUpdateResult(false, rowId, rowId);
        }

        List<(TableIndex Index, IndexKey OldKey, IndexKey NewKey, bool Removed, bool Added)> mutations = [];
        try
        {
            foreach (var index in _indexes)
            {
                var oldKey = CatalogIndexKey.Encode(oldRow, _table, index.Definition);
                var newKey = CatalogIndexKey.Encode(newRow, _table, index.Definition);
                if (!relocated && oldKey.Equals(newKey)) continue;
                if (!await index.RemoveAsync(oldKey, rowId, cancellationToken).ConfigureAwait(false))
                    throw new StorageCorruptionException($"Index {index.Definition.Id} is missing the row being updated.");
                mutations.Add((index, oldKey, newKey, true, false));
                await index.AddAsync(newKey, newRowId, cancellationToken).ConfigureAwait(false);
                mutations[^1] = mutations[^1] with { Added = true };
            }
            if (relocated && !await _heap.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false))
                throw new StorageCorruptionException("Relocated row's previous heap slot could not be deleted.");
            foreach (var reference in replacement.Retired) await _overflow.FreeAsync(reference, cancellationToken).ConfigureAwait(false);
            return new TableUpdateResult(true, rowId, newRowId);
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            for (var index = mutations.Count - 1; index >= 0; index--)
            {
                var mutation = mutations[index];
                try
                {
                    if (mutation.Added) await mutation.Index.RemoveAsync(mutation.NewKey, newRowId).ConfigureAwait(false);
                    if (mutation.Removed) await mutation.Index.AddAsync(mutation.OldKey, rowId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (StorageException) { unreclaimed.Add(mutation.Index.Definition.RootPageId); }
            }
            try
            {
                if (relocated) await _heap.DeleteAsync(newRowId).ConfigureAwait(false);
                else if (await _heap.UpdateAsync(rowId, oldBytes).ConfigureAwait(false) != HeapUpdateResult.Updated)
                    unreclaimed.Add(rowId.PageId);
            }
            catch (StorageException) { unreclaimed.Add(newRowId.PageId); }
            foreach (var reference in replacement.NewlyAllocated)
                try { await _overflow.FreeAsync(reference).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(reference.FirstPageId); }
            throw new TableMutationException("Table update failed and the previous logical state was restored.",
                unreclaimed.Distinct().ToArray(), exception);
        }
    }
}

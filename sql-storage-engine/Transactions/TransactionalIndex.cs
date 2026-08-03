using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Applies serializable predicate locks and insertion intent around a persistent index.</summary>
public sealed class TransactionalIndex
{
    private readonly PersistentBPlusTree _tree;
    private readonly IndexId _indexId;
    private readonly LockingTransaction _transaction;
    private readonly TransactionIsolationLevel _isolationLevel;

    public TransactionalIndex(PersistentBPlusTree tree, IndexId indexId, LockingTransaction transaction,
        TransactionIsolationLevel isolationLevel)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        if (!Enum.IsDefined(isolationLevel)) throw new ArgumentOutOfRangeException(nameof(isolationLevel));
        _indexId = indexId;
        _isolationLevel = isolationLevel;
    }

    /// <summary>Materializes a range scan while retaining its shared predicate lock for serializable transactions.</summary>
    public async ValueTask<IReadOnlyList<LeafIndexEntry>> ScanAsync(IndexRange range,
        CancellationToken cancellationToken = default)
    {
        var resource = IndexRangeLockResource.From(_indexId, range);
        await _transaction.AcquireAsync(resource, LockMode.Shared, cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = new List<LeafIndexEntry>();
            await foreach (var entry in _tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
                entries.Add(entry);
            return entries.AsReadOnly();
        }
        finally
        {
            if (_isolationLevel != TransactionIsolationLevel.Serializable) _transaction.Release(resource);
        }
    }

    /// <summary>Inserts under an exclusive key intent retained until transaction completion.</summary>
    public async ValueTask InsertAsync(IndexKey key, RowId rowId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _transaction.AcquireAsync(new IndexKeyLockResource(_indexId, key), LockMode.Exclusive,
            cancellationToken).ConfigureAwait(false);
        await _tree.InsertAsync(key, rowId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes under an exclusive key intent retained until transaction completion.</summary>
    public async ValueTask<IndexDeleteResult> DeleteAsync(IndexKey key, RowId rowId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _transaction.AcquireAsync(new IndexKeyLockResource(_indexId, key), LockMode.Exclusive,
            cancellationToken).ConfigureAwait(false);
        return await _tree.DeleteAsync(key, rowId, cancellationToken).ConfigureAwait(false);
    }
}

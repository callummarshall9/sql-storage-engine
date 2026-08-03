using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;

namespace sql_storage_engine.Transactions;

/// <summary>Defines the row-lock retention policy used by a transactional table session.</summary>
public enum TransactionIsolationLevel
{
    ReadCommitted = 1,
    RepeatableRead = 2,
    Serializable = 3
}

/// <summary>Applies transaction-owned row locks around logical table reads and mutations.</summary>
public sealed class TransactionalTable
{
    private readonly TableStorage _table;
    private readonly LockingTransaction _transaction;
    private readonly Dictionary<RowLockResource, LockMode> _heldRows = [];

    public TransactionalTable(TableStorage table, LockingTransaction transaction,
        TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.ReadCommitted)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        if (!Enum.IsDefined(isolationLevel)) throw new ArgumentOutOfRangeException(nameof(isolationLevel));
        IsolationLevel = isolationLevel;
    }

    public TransactionIsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Reads a row under a shared lock. Read-committed releases the lock after the read; repeatable-read and
    /// serializable retain it until transaction completion.
    /// </summary>
    public async ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(rowId);
        var acquiredForRead = !_heldRows.ContainsKey(resource);
        if (acquiredForRead)
        {
            await _transaction.AcquireAsync(resource, LockMode.Shared, cancellationToken).ConfigureAwait(false);
            _heldRows[resource] = LockMode.Shared;
        }
        try { return await _table.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false); }
        finally
        {
            if (acquiredForRead && IsolationLevel == TransactionIsolationLevel.ReadCommitted)
            {
                _transaction.Release(resource);
                _heldRows.Remove(resource);
            }
        }
    }

    /// <summary>Updates a row under an exclusive lock retained until transaction completion.</summary>
    public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(rowId);
        await AcquireExclusiveAsync(resource, cancellationToken).ConfigureAwait(false);
        var result = await _table.UpdateAsync(rowId, update, cancellationToken).ConfigureAwait(false);
        if (result.Relocated)
            await AcquireExclusiveAsync(Resource(result.CurrentRowId), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Deletes a row under an exclusive lock retained until transaction completion.</summary>
    public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId,
        CancellationToken cancellationToken = default)
    {
        await AcquireExclusiveAsync(Resource(rowId), cancellationToken).ConfigureAwait(false);
        return await _table.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false);
    }

    private RowLockResource Resource(RowId rowId) => new(_table.Definition.Id, rowId);

    private async ValueTask AcquireExclusiveAsync(RowLockResource resource, CancellationToken cancellationToken)
    {
        if (_heldRows.TryGetValue(resource, out var current) && current == LockMode.Shared)
            await _transaction.ConvertAsync(resource, LockMode.Exclusive, cancellationToken).ConfigureAwait(false);
        else
            await _transaction.AcquireAsync(resource, LockMode.Exclusive, cancellationToken).ConfigureAwait(false);
        _heldRows[resource] = LockMode.Exclusive;
    }
}

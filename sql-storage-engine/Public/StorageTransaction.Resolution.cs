using sql_storage_engine.Transactions;

namespace sql_storage_engine;

internal sealed partial class StorageTransaction
{
    public async ValueTask<StorageTransactionReceipt> CommitAsync(CancellationToken cancellationToken = default)
    {
        EnterOperation();
        try
        {
            if (State == StorageTransactionState.Committed) return new(Identity, State);
            if (State != StorageTransactionState.Committable) throw new InvalidOperationException("Transaction cannot commit.");
            using var scope = _lease.Activate();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, cancellationToken);
            var decisionStarted = false;
            try
            {
                await _owner.FlushTransactionAsync(linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                _owner.TransactionObserver?.Invoke("BeforeCommitDecision");
                decisionStarted = true;
                await _journal.MarkCommittedAsync(CancellationToken.None, retain: true).ConfigureAwait(false);
                _owner.TransactionObserver?.Invoke("AfterCommitDecision");
                await CompleteCoreAsync(StorageTransactionState.Committed).ConfigureAwait(false);
            }
            catch
            {
                if (decisionStarted) MarkIndeterminate();
                else await AbortCoreAsync().ConfigureAwait(false);
            }
            return new(Identity, State);
        }
        finally { _operation.Release(); }
    }

    public async ValueTask<StorageTransactionReceipt> RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnterOperation();
        try
        {
            if (State == StorageTransactionState.Aborted) return new(Identity, State);
            if (State is not (StorageTransactionState.Committable or StorageTransactionState.Doomed)) throw new InvalidOperationException("Transaction cannot roll back.");
            await AbortCoreAsync().ConfigureAwait(false);
            return new(Identity, State);
        }
        finally { _operation.Release(); }
    }

    private async ValueTask AbortCoreAsync()
    {
        try
        {
            using var scope = _lease.Activate();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _owner.TransactionObserver?.Invoke("BeforeRollback");
            await _owner.RestoreTransactionAsync(_journal, cleanup.Token).ConfigureAwait(false);
            await CompleteCoreAsync(StorageTransactionState.Aborted).ConfigureAwait(false);
        }
        catch { MarkIndeterminate(); }
    }

    private async ValueTask CompleteCoreAsync(StorageTransactionState state)
    {
        await StorageTransactionFiles.WriteReceiptAsync(_owner.TransactionPath, new(Identity, state), CancellationToken.None).ConfigureAwait(false);
        _owner.TransactionObserver?.Invoke("AfterReceipt");
        StorageSavepointFiles.DeleteAll(_owner.TransactionPath);
        _journal.Delete();
        TransactionDirectory.Delete(StorageTransactionFiles.ActivePath(_owner.TransactionPath));
        State = state;
        Release(false);
    }

    private void MarkIndeterminate()
    {
        State = StorageTransactionState.Indeterminate;
        Release(true);
    }

    private void Release(bool quarantine)
    {
        if (_released) return;
        _released = true;
        _owner.FinishTransaction(this, quarantine);
        _lease.Dispose();
    }
}

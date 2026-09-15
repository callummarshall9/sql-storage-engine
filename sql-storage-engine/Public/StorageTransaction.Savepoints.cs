using sql_storage_engine.Transactions;

namespace sql_storage_engine;

internal sealed partial class StorageTransaction
{
    public async ValueTask<StorageSavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        EnterOperation();
        try
        {
            EnsureSavepointState();
            using var scope = _lease.Activate();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, cancellationToken);
            return await _savepoints.CreateAsync(name, linked.Token).ConfigureAwait(false);
        }
        catch (AggregateException) { State = StorageTransactionState.Doomed; throw; }
        finally { _operation.Release(); }
    }

    public ValueTask RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        => RollbackSavepointAsync(null, name, cancellationToken);

    public ValueTask RollbackToSavepointAsync(StorageSavepoint savepoint, CancellationToken cancellationToken = default)
        => RollbackSavepointAsync(savepoint, null, cancellationToken);

    private async ValueTask RollbackSavepointAsync(StorageSavepoint? savepoint, string? name, CancellationToken cancellationToken)
    {
        EnterOperation();
        try
        {
            EnsureSavepointState();
            using var scope = _lease.Activate();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, cancellationToken);
            await _savepoints.RollbackAsync(name is null ? savepoint! : _savepoints.Find(name), linked.Token).ConfigureAwait(false);
        }
        catch (AggregateException) { State = StorageTransactionState.Doomed; throw; }
        finally { _operation.Release(); }
    }

    private void EnsureSavepointState()
    {
        if (State != StorageTransactionState.Committable) throw new InvalidOperationException("Savepoints require a committable transaction.");
    }
}

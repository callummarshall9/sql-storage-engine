namespace sql_storage_engine;

/// <summary>An exclusive Serializable root; callbacks enlist reads and mutations in the same durable lifetime.</summary>
public interface IStorageTransaction : IAsyncDisposable
{
    StorageTransactionIdentity Identity { get; }
    StorageTransactionState State { get; }
    ValueTask ExecuteStatementAsync(Func<IStorageTransactionContext, CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default);
    ValueTask<StorageSavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support savepoints.");
    ValueTask RollbackToSavepointAsync(StorageSavepoint savepoint, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support savepoints.");
    ValueTask RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This provider does not support savepoints.");
    ValueTask<StorageTransactionReceipt> CommitAsync(CancellationToken cancellationToken = default);
    ValueTask<StorageTransactionReceipt> RollbackAsync(CancellationToken cancellationToken = default);
}

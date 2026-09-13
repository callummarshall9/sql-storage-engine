namespace sql_storage_engine;

/// <summary>An exclusive Serializable root; callbacks enlist reads and mutations in the same durable lifetime.</summary>
public interface IStorageTransaction : IAsyncDisposable
{
    StorageTransactionIdentity Identity { get; }
    StorageTransactionState State { get; }
    ValueTask ExecuteStatementAsync(Func<IStorageTransactionContext, CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default);
    ValueTask<StorageTransactionReceipt> CommitAsync(CancellationToken cancellationToken = default);
    ValueTask<StorageTransactionReceipt> RollbackAsync(CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine;
/// <summary>An exclusive Serializable root; callbacks enlist reads and mutations in the same durable lifetime.</summary>
public interface IStorageTransaction : IAsyncDisposable
{
    StorageTransactionIdentity ReadIdentity();

    StorageTransactionState ReadState();

    ValueTask ExecuteStatementAsync(Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default);

    ValueTask<StorageSavepoint> CreateStorageSavepointAsync(string name, CancellationToken cancellationToken = default);

    ValueTask RollbackToSavepointAsync(StorageSavepoint savepoint, CancellationToken cancellationToken = default);

    ValueTask RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<StorageTransactionReceipt> CommitAsync(CancellationToken cancellationToken = default);

    ValueTask<StorageTransactionReceipt> RollbackAsync(CancellationToken cancellationToken = default);
}
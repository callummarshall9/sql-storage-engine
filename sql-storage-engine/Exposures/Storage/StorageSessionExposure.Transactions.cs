// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    public async ValueTask<IStorageTransaction> BeginTransactionAsync(StorageTransactionOptions storageTransactionOptions, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(storageSessionContext: (await service.BeginStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, TransactionRequest = new StorageTransactionRequestContext { Options = storageTransactionOptions } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TransactionRequest!.Result!);

    public StorageTransactionIdentity ReadIdentity() =>
        service.ReadStorageSessionContextTransaction(storageSessionContext: sessionContext)
            .Transaction!.Identity;

    public StorageTransactionState ReadState() =>
        service.ReadStorageSessionContextTransaction(storageSessionContext: sessionContext)
            .Transaction!.State;

    ValueTask IStorageTransaction.ExecuteStatementAsync(Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken) =>
        new ValueTask(task: service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "Execute", CallbackSource = operation, Callback = (context, token) => operation(arg1: new StorageSessionExposure(storageSessionContext: context), arg2: token) } }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<StorageSavepoint> CreateStorageSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "CreateSavepoint", Name = name } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TransactionRequest!.SavepointResult!;

    public ValueTask RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "RollbackSavepoint", Name = name } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask RollbackToSavepointAsync(StorageSavepoint storageSavepoint, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "RollbackSavepoint", Savepoint = storageSavepoint } }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<StorageTransactionReceipt> CommitAsync(CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "Commit" } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TransactionRequest!.Receipt!;

    public async ValueTask<StorageTransactionReceipt> RollbackAsync(CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextTransactionAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, TransactionRequest = new StorageTransactionRequestContext { Operation = "Rollback" } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TransactionRequest!.Receipt!;

    public async ValueTask<StorageTransactionReceipt> ResolveTransactionAsync(StorageTransactionIdentity storageTransactionIdentity, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextReceiptAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, TransactionRequest = new StorageTransactionRequestContext { Operation = "Resolve", Identity = storageTransactionIdentity } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TransactionRequest!.Receipt!;

    public ValueTask ReleaseTransactionReceiptAsync(StorageTransactionIdentity storageTransactionIdentity, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextReceiptAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, TransactionRequest = new StorageTransactionRequestContext { Operation = "Release", Identity = storageTransactionIdentity } }, cancellationToken: cancellationToken)
            .AsTask());
}
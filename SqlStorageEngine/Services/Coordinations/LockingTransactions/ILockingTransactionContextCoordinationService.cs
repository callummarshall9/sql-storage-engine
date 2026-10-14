// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Coordinations.LockingTransactions;
internal interface ILockingTransactionContextCoordinationService
{
    void RegisterLockingTransactionContext(LockingTransactionContext lockingTransactionContext);

    ValueTask AcquireLockingTransactionContextAsync(LockingTransactionContext lockingTransactionContext, CancellationToken cancellationToken);

    ValueTask ConvertLockingTransactionContextAsync(LockingTransactionContext lockingTransactionContext, CancellationToken cancellationToken);

    bool ReleaseLockingTransactionContext(LockingTransactionContext lockingTransactionContext);

    void CommitLockingTransactionContext(LockingTransactionContext lockingTransactionContext);

    void RollbackLockingTransactionContext(LockingTransactionContext lockingTransactionContext);

    void CloseLockingTransactionContext(LockingTransactionContext lockingTransactionContext);

    LockingTransactionContext ReadLockingTransactionContext(LockingTransactionContext lockingTransactionContext);
}
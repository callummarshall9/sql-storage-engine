// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Orchestrations.LockManagers;
using sql_storage_engine.Services.Orchestrations.TransactionLifecycles;

namespace sql_storage_engine.Services.Coordinations.LockingTransactions;
internal sealed partial class LockingTransactionContextCoordinationService(LockManagerContextOrchestrationService locks, TransactionLifecycleOrchestrationService lifecycles) : ILockingTransactionContextCoordinationService
{
    internal static LockingTransactionContextCoordinationService CreateLockingTransactionContextCoordinationService(LockManagerBinding lockManagerBinding) =>
        new LockingTransactionContextCoordinationService(locks: (LockManagerContextOrchestrationService)lockManagerBinding.Service!, lifecycles: TransactionLifecycleOrchestrationService.CreateTransactionLifecycleOrchestrationService());

    public LockingTransactionContext ReadLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);

        return lockingTransactionContext with
        {
            TransactionId = Id(lockingTransactionContext: lockingTransactionContext),
            Phase = lifecycles.ReadTransactionLifecycleState(transactionLifecycle: lockingTransactionContext.Lifecycle!)
        };
    });

    public void RegisterLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);
        locks.RegisterLockManagerContextVictimHandler(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext), VictimHandler = () => AbortForDeadlock(lockingTransactionContext: lockingTransactionContext) });
    });

    public ValueTask AcquireLockingTransactionContextAsync(LockingTransactionContext lockingTransactionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext, cancellationToken: cancellationToken);
        lifecycles.EnsureActiveTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);

        await locks.AcquireLockManagerContextAsync(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext), LockResource = lockingTransactionContext.Resource, Mode = lockingTransactionContext.Mode }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask ConvertLockingTransactionContextAsync(LockingTransactionContext lockingTransactionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext, cancellationToken: cancellationToken);
        lifecycles.EnsureActiveTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);

        await locks.ConvertLockManagerContextAsync(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext), LockResource = lockingTransactionContext.Resource, Mode = lockingTransactionContext.Mode }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public bool ReleaseLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);
        return locks.ReleaseLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext), LockResource = lockingTransactionContext.Resource });
    });

    public void CommitLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);

        try
        {
            lifecycles.CommitTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);
        }
        finally
        {
            locks.ReleaseAllLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext) });
        }
    });

    public void RollbackLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);

        try
        {
            lifecycles.RollbackTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);
        }
        finally
        {
            locks.ReleaseAllLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext) });
        }
    });

    public void CloseLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);

        try
        {
            lifecycles.CloseTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);
        }
        finally
        {
            locks.ReleaseAllLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = Id(lockingTransactionContext: lockingTransactionContext) });
        }
    });

    private TransactionId Id(LockingTransactionContext lockingTransactionContext) =>
        new TransactionId
    {
        Value = lifecycles.ReadTransactionLifecycleId(transactionLifecycle: lockingTransactionContext.Lifecycle!)
    };

    private void AbortForDeadlock(LockingTransactionContext lockingTransactionContext)
    {
        try
        {
            if (lifecycles.ReadTransactionLifecycleState(transactionLifecycle: lockingTransactionContext.Lifecycle!) == TransactionState.Active)
            {
                lifecycles.RollbackTransactionLifecycle(transactionLifecycle: lockingTransactionContext.Lifecycle!);
            }
        }
        finally
        {
            lockingTransactionContext.ReleasePins?.Invoke();
        }
    }
}
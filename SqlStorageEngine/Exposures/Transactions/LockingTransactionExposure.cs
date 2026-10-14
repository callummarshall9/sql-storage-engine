// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Coordinations.LockingTransactions;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class LockingTransactionExposure : IDisposable, ICompositionExposure, ILockingTransactionExposure
{
    internal readonly LockingTransactionContextCoordinationService service;
    internal readonly LockingTransactionContext state;
    public LockingTransactionExposure(ITransactionLifecycleExposure transaction, ILockManagerExposure lockManager, Action? releasePins = null)
    {
        state = new LockingTransactionContext
        {
            Lifecycle = transaction.ReadTransactionLifecycle(),
            ReleasePins = releasePins
        };
        service = LockingTransactionContextCoordinationService.CreateLockingTransactionContextCoordinationService(lockManagerBinding: lockManager.ReadLockManagerBinding());
        service.RegisterLockingTransactionContext(lockingTransactionContext: state);
    }

    public sql_storage_engine.Models.Identifiers.TransactionId ReadId() =>
        service.ReadLockingTransactionContext(lockingTransactionContext: state)
            .TransactionId;

    public TransactionState ReadState() =>
        service.ReadLockingTransactionContext(lockingTransactionContext: state)
            .Phase;

    public ValueTask AcquireAsync(LockResource lockResource, LockMode mode, CancellationToken cancellationToken = default) =>
        service.AcquireLockingTransactionContextAsync(lockingTransactionContext: state with { Resource = lockResource, Mode = mode }, cancellationToken: cancellationToken);

    public ValueTask ConvertAsync(LockResource lockResource, LockMode mode, CancellationToken cancellationToken = default) =>
        service.ConvertLockingTransactionContextAsync(lockingTransactionContext: state with { Resource = lockResource, Mode = mode }, cancellationToken: cancellationToken);

    public bool Release(LockResource lockResource) =>
        service.ReleaseLockingTransactionContext(lockingTransactionContext: state with { Resource = lockResource });

    public void Commit() =>
        service.CommitLockingTransactionContext(lockingTransactionContext: state);

    public void Rollback() =>
        service.RollbackLockingTransactionContext(lockingTransactionContext: state);

    public void Dispose() =>
        service.CloseLockingTransactionContext(lockingTransactionContext: state);

    public LockingTransactionBinding ReadLockingTransactionBinding() =>
        new LockingTransactionBinding
    {
        Service = service,
        State = state
    };
}
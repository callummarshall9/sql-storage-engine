// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Orchestrations.LockManagers;
using sql_storage_engine.Services.Processings.LockResources;
using sql_storage_engine.Services.Processings.LockRules;
using sql_storage_engine.Services.Foundations.IndexKeys;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class LockManagerExposure : ILockManagerExposure, cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    internal readonly ILockManagerContextOrchestrationService service = new LockManagerContextOrchestrationService(lockRelations: new IndexKeyLockResourceProcessingService(keys: new IndexKeyService()), lockRules: new LockRulesProcessingService());
    public long ReadDeadlockCount() =>
        service.ReadLockManagerContextDeadlockCount(lockManagerContext: new LockManagerContext());

    public TransactionId? ReadLastDeadlockVictim() =>
        service.ReadLockManagerContextLastVictim(lockManagerContext: new LockManagerContext())
            .LastVictim;

    public void RegisterVictimHandler(TransactionId transactionId, Action handler) =>
        service.RegisterLockManagerContextVictimHandler(lockManagerContext: new LockManagerContext { TransactionId = transactionId, VictimHandler = handler });

    public ValueTask AcquireAsync(TransactionId transactionId, LockResource lockResource, LockMode mode, CancellationToken cancellationToken = default) =>
        service.AcquireLockManagerContextAsync(lockManagerContext: new LockManagerContext { TransactionId = transactionId, LockResource = lockResource, Mode = mode }, cancellationToken: cancellationToken);

    public ValueTask ConvertAsync(TransactionId transactionId, LockResource lockResource, LockMode mode, CancellationToken cancellationToken = default) =>
        service.ConvertLockManagerContextAsync(lockManagerContext: new LockManagerContext { TransactionId = transactionId, LockResource = lockResource, Mode = mode }, cancellationToken: cancellationToken);

    public bool Release(TransactionId transactionId, LockResource lockResource) =>
        service.ReleaseLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = transactionId, LockResource = lockResource });

    public void ReleaseAll(TransactionId transactionId) =>
        service.ReleaseAllLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = transactionId });

    public LockManagerBinding ReadLockManagerBinding() =>
        new LockManagerBinding
    {
        Service = service
    };
}
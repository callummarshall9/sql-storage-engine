// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Orchestrations.LockManagers;
internal interface ILockManagerContextOrchestrationService
{
    void RegisterLockManagerContextVictimHandler(LockManagerContext lockManagerContext);

    ValueTask AcquireLockManagerContextAsync(LockManagerContext lockManagerContext, CancellationToken cancellationToken);

    ValueTask ConvertLockManagerContextAsync(LockManagerContext lockManagerContext, CancellationToken cancellationToken);

    bool ReleaseLockManagerContext(LockManagerContext lockManagerContext);

    void ReleaseAllLockManagerContext(LockManagerContext lockManagerContext);

    long ReadLockManagerContextDeadlockCount(LockManagerContext lockManagerContext);

    LockManagerContext ReadLockManagerContextLastVictim(LockManagerContext lockManagerContext);
}
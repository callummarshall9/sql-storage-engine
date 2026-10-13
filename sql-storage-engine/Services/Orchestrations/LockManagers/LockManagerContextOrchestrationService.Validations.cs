// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Orchestrations.LockManagers;
internal sealed partial class LockManagerContextOrchestrationService
{
    private static void ValidateRequest(TransactionId transactionId, LockResource resource, LockMode mode)
    {
        if (transactionId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(transactionId));
        }

        ArgumentNullException.ThrowIfNull(argument: resource);

        if (!Enum.IsDefined(value: mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void ValidateContext(LockManagerContext lockManagerContext) =>
        ArgumentNullException.ThrowIfNull(argument: lockManagerContext);

    private static void ValidateVictimHandler(LockManagerContext lockManagerContext)
    {
        ValidateContext(lockManagerContext: lockManagerContext);
        ArgumentNullException.ThrowIfNull(argument: lockManagerContext.VictimHandler);

        if (lockManagerContext.TransactionId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: "transactionId");
        }
    }

    private static void ValidateAcquisition(LockManagerContext lockManagerContext, CancellationToken cancellationToken)
    {
        ValidateContext(lockManagerContext: lockManagerContext);
        ValidateRequest(transactionId: lockManagerContext.TransactionId, resource: lockManagerContext.LockResource!, mode: lockManagerContext.Mode);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRelease(LockManagerContext lockManagerContext)
    {
        ValidateContext(lockManagerContext: lockManagerContext);
        ArgumentNullException.ThrowIfNull(argument: lockManagerContext.LockResource);
    }
}
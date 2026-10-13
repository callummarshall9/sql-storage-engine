// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.RecoveryRequirements;
/// <summary>Synchronizes reads and monotonic updates of the in-process recovery requirement.</summary>
internal sealed partial class RecoveryRequirementService : IRecoveryRequirementService
{
    public bool ReadRecoveryRequirement(RecoveryRequirement recoveryRequirement) =>
        TryCatch(operation: () =>
    {
        ValidateRecoveryRequirementOnRead(recoveryRequirement: recoveryRequirement);
        return Volatile.Read(location: ref recoveryRequirement.Required) != 0;
    });

    public void MarkRecoveryRequirement(RecoveryRequirement recoveryRequirement) =>
        TryCatch(operation: () =>
    {
        ValidateRecoveryRequirementOnMark(recoveryRequirement: recoveryRequirement);
        Interlocked.Exchange(location1: ref recoveryRequirement.Required, value: 1);
    });
}
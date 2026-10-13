// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Foundations.RecoveryRequirements;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class RecoveryRequirementExposure : IRecoveryRequirementExposure
{
    private readonly IRecoveryRequirementService service = new RecoveryRequirementService();
    private readonly RecoveryRequirement recoveryRequirement = new();
    public bool IsRecoveryRequired() =>
        service.ReadRecoveryRequirement(recoveryRequirement: recoveryRequirement);

    public void MarkRecoveryRequired() =>
        service.MarkRecoveryRequirement(recoveryRequirement: recoveryRequirement);
}
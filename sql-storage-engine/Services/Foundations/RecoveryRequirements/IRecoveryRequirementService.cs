// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.RecoveryRequirements;
internal interface IRecoveryRequirementService
{
    bool ReadRecoveryRequirement(RecoveryRequirement recoveryRequirement);

    void MarkRecoveryRequirement(RecoveryRequirement recoveryRequirement);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.RecoveryRequirements;
internal sealed partial class RecoveryRequirementService
{
    private static void ValidateRecoveryRequirementOnRead(RecoveryRequirement recoveryRequirement) =>
        Validate(inputs: [(recoveryRequirement is null, nameof(recoveryRequirement))]);

    private static void ValidateRecoveryRequirementOnMark(RecoveryRequirement recoveryRequirement) =>
        Validate(inputs: [(recoveryRequirement is null, nameof(recoveryRequirement))]);

    private static void Validate(ReadOnlySpan<(bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
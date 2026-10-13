// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityWal;
internal sealed partial class CompatibilityWalOrchestrationService
{
    private static void ValidateCompatibilityContext(CompatibilityContext compatibilityContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(compatibilityContext is null, nameof(compatibilityContext)), (compatibilityContext?.Fixture is null, nameof(CompatibilityContext.Fixture))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A bounded compatibility request is required.", paramName: input.ParameterName);
            }
        }
    }
}
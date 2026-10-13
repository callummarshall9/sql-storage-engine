// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityFiles;
internal sealed partial class CompatibilityFileOrchestrationService
{
    private static void ValidateCompatibilityContext(CompatibilityContext compatibilityContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(compatibilityContext is null, nameof(compatibilityContext)), (string.IsNullOrWhiteSpace(value: compatibilityContext?.FixturePath), nameof(CompatibilityContext.FixturePath)), (string.IsNullOrWhiteSpace(value: compatibilityContext?.DatabasePath), nameof(CompatibilityContext.DatabasePath))]);
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
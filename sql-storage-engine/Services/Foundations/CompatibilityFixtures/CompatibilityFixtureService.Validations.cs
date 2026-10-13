// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.CompatibilityFixtures;
internal sealed partial class CompatibilityFixtureService
{
    private static void ValidateCompatibilityContext(CompatibilityContext compatibilityContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(compatibilityContext is null, nameof(compatibilityContext)), (compatibilityContext?.Bytes is null, nameof(CompatibilityContext.Bytes)), (compatibilityContext?.Bytes?.Length is <= 0 or > 1024 * 1024, nameof(CompatibilityContext.Bytes))]);
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
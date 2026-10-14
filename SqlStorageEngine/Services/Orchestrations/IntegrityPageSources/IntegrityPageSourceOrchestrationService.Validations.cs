// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.IntegrityPageSources;
internal sealed partial class IntegrityPageSourceOrchestrationService
{
    private static void ValidateIntegrityContext(IntegrityContext integrityContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(integrityContext is null, nameof(integrityContext)), (integrityContext?.Source is null, nameof(IntegrityContext.Source))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
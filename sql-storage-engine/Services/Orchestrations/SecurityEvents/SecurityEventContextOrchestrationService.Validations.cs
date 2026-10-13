// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityEvents;
internal sealed partial class SecurityEventContextOrchestrationService
{
    private static void ValidateSecurityEventContext(SecurityEventContext securityEventContext, CancellationToken token)
    {
        Validate(inputs: [(securityEventContext is null, nameof(securityEventContext)), (string.IsNullOrWhiteSpace(value: securityEventContext?.TransactionPath), nameof(SecurityEventContext.TransactionPath))]);
        token.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A security event context is required.", paramName: input.ParameterName);
            }
        }
    }
}
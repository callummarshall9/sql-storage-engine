// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Orchestrations.OverflowChains;
internal sealed partial class OverflowChainContextOrchestrationService
{
    private static void ValidateWrite(OverflowChainContext overflowChainContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: overflowChainContext);

        if (overflowChainContext.Value.IsEmpty || overflowChainContext.Value.Length > OverflowReferenceFormat.MaximumValueLength)
        {
            throw new ArgumentException(message: "Overflow value length is outside the supported bound.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRead(OverflowChainContext overflowChainContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: overflowChainContext);
        ArgumentNullException.ThrowIfNull(argument: overflowChainContext.Reference);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
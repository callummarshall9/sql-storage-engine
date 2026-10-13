// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.HeapMaintenanceCheckpoints;
internal sealed partial class HeapMaintenanceCheckpointService
{
    private static void ValidateHeapMaintenanceProgressOnGet(CancellationToken cancellationToken) =>
        Validate(inputs: [(cancellationToken.IsCancellationRequested, cancellationToken)]);

    private static void ValidateHeapMaintenanceProgressOnSet(HeapMaintenanceProgress heapMaintenanceProgress, CancellationToken cancellationToken)
    {
        Validate(inputs: [(cancellationToken.IsCancellationRequested, cancellationToken)]);
        ArgumentNullException.ThrowIfNull(argument: heapMaintenanceProgress);
        Validate(inputs: [(heapMaintenanceProgress.NextPageIndex < 0, nameof(heapMaintenanceProgress.NextPageIndex)), (heapMaintenanceProgress.ReclaimedBytes < 0, nameof(heapMaintenanceProgress.ReclaimedBytes)), (heapMaintenanceProgress.FailureCount < 0, nameof(heapMaintenanceProgress.FailureCount))]);
    }

    private static void Validate(ReadOnlySpan<(bool Cancelled, CancellationToken Token)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Cancelled)
            {
                throw new OperationCanceledException(token: input.Token);
            }
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
        }
    }
}
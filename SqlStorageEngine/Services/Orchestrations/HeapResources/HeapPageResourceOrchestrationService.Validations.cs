// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Orchestrations.HeapResources;
internal sealed partial class HeapPageResourceOrchestrationService
{
    private static void ValidateResource()
    {
    }

    private static void ValidateCancellation(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePage(ulong pageId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value: pageId);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateLease(HeapPageLease heapPageLease)
    {
        ArgumentNullException.ThrowIfNull(argument: heapPageLease);

        if (heapPageLease.Id == Guid.Empty)
        {
            throw new ArgumentException(message: "Heap lease identity is empty.");
        }
    }

    private static void ValidateLeaseMark(HeapPageLease heapPageLease, ulong logSequenceNumber)
    {
        ValidateLease(heapPageLease: heapPageLease);
    }

    private static void ValidateRequiredBytes(int requiredBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: requiredBytes);
    }

    private static void ValidateFreeSpacePage(ulong pageId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value: pageId);
    }

    private static void ValidateHeapPageResourceFreeSpaceOnUpdate(ulong pageId, int freeBytes) =>
        Validate(inputs: [(pageId == 0, nameof(pageId)), (freeBytes < 0, nameof(freeBytes))]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }
}
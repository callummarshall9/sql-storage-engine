// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Foundations.HeapResources;
internal sealed partial class HeapPageResourceService
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

    private static void ValidateWrite(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        if (source.IsEmpty)
        {
            throw new ArgumentException(message: "Overflow image cannot be empty.");
        }
    }

    private static void ValidateConfiguration(HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.BufferSource);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.AllocatorSource);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.PageSize);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Pin);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Allocate);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Discard);
        ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Free);

        if (heapPageResourceConfiguration.FreeSpaceRequired)
        {
            ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.FreeSpaceSource);
        }

        if (heapPageResourceConfiguration.FreeSpaceSource is not null)
        {
            ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Find);
            ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Update);
            ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Remove);
            ArgumentNullException.ThrowIfNull(argument: heapPageResourceConfiguration.Clear);
        }
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
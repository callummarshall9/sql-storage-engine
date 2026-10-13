// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Foundations.OverflowPages;
internal sealed partial class OverflowPageResourceService
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

    private static void ValidateConfiguration(OverflowPageResourceConfiguration overflowPageResourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.BufferSource);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.AllocatorSource);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.PageSize);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.Pin);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.Allocate);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.Discard);
        ArgumentNullException.ThrowIfNull(argument: overflowPageResourceConfiguration.Free);
    }
}
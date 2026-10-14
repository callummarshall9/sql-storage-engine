// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Services.Processings.OverflowPages;
internal sealed partial class OverflowPageResourceProcessingService
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
}
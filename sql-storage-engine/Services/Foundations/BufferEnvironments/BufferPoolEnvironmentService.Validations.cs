// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Foundations.BufferEnvironments;
internal sealed partial class BufferPoolEnvironmentService
{
    private static void ValidateConfiguration(BufferPoolContext bufferPoolContext)
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        var configuration = bufferPoolContext.Configuration!;
        ArgumentNullException.ThrowIfNull(argument: configuration.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: configuration.PageSize);
        ArgumentNullException.ThrowIfNull(argument: configuration.Read);
        ArgumentNullException.ThrowIfNull(argument: configuration.Write);
        ArgumentNullException.ThrowIfNull(argument: configuration.Flush);
        ArgumentNullException.ThrowIfNull(argument: configuration.Close);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: configuration.Capacity);
    }

    private static void ValidateContext(BufferPoolContext bufferPoolContext)
    {
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext);
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext.Configuration);
    }

    private static void ValidateCancellation(BufferPoolContext bufferPoolContext, CancellationToken cancellationToken)
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateOffset(BufferPoolContext bufferPoolContext, ulong pageId, CancellationToken cancellationToken)
    {
        ValidateCancellation(bufferPoolContext: bufferPoolContext, cancellationToken: cancellationToken);

        if (bufferPoolContext.PageSize <= 0 || pageId > (ulong)(long.MaxValue / bufferPoolContext.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private static void ValidateRead(BufferPoolContext bufferPoolContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ValidateOffset(bufferPoolContext: bufferPoolContext, pageId: pageId, cancellationToken: cancellationToken);

        if (destination.Length != bufferPoolContext.PageSize)
        {
            throw new ArgumentException(message: "A complete page is required.");
        }
    }

    private static void ValidateWrite(BufferPoolContext bufferPoolContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidateOffset(bufferPoolContext: bufferPoolContext, pageId: pageId, cancellationToken: cancellationToken);

        if (source.Length != bufferPoolContext.PageSize)
        {
            throw new ArgumentException(message: "A complete page is required.");
        }
    }

    private static void ValidateGuard(BufferPoolContext bufferPoolContext, ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        ValidateOffset(bufferPoolContext: bufferPoolContext, pageId: pageId, cancellationToken: cancellationToken);
}
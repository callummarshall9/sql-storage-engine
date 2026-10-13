// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Orchestrations.BufferPools;
internal sealed partial class BufferPoolContextOrchestrationService
{
    private static void ValidateCreation(BufferPoolContext bufferPoolContext)
    {
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext);
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext.Configuration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: bufferPoolContext.Configuration.Capacity);
    }

    private void ValidateState()
    {
        ArgumentNullException.ThrowIfNull(argument: state);
        ArgumentNullException.ThrowIfNull(argument: state.Runtime);
    }

    private void ValidateCancellation(CancellationToken cancellationToken)
    {
        ValidateState();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidatePage(ulong pageId, CancellationToken cancellationToken)
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        if (state.PageSize <= 0 || pageId > (ulong)(long.MaxValue / state.PageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageId));
        }
    }

    private void ValidatePin(BufferPoolContext bufferPoolContext)
    {
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext);
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext.ResultFrame);

        if (!ReferenceEquals(objA: bufferPoolContext.Runtime, objB: state.Runtime))
        {
            throw new ArgumentException(message: "Pin belongs to a different buffer pool.");
        }

        if (bufferPoolContext.ResultPinId is null or 0)
        {
            throw new ArgumentException(message: "A pinned page view is required.");
        }
    }

    private void ValidatePinMutation(BufferPoolContext bufferPoolContext, ulong logSequenceNumber) =>
        ValidatePin(bufferPoolContext: bufferPoolContext);

    private void ValidateClose(BufferPoolContext? bufferPoolContext)
    {
        ValidateState();

        if (bufferPoolContext is not null)
        {
            ValidatePin(bufferPoolContext: bufferPoolContext);
        }
    }
}
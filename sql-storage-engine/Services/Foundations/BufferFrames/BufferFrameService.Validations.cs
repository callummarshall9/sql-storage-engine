// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Foundations.BufferFrames;
internal sealed partial class BufferFrameService
{
    private static void ValidateBufferFrameStateReset(BufferFrameState bufferFrameState, ulong pageId, byte[] bytes)
    {
        ValidateBufferFrameState(bufferFrameState: bufferFrameState);
        ArgumentNullException.ThrowIfNull(argument: bytes);

        if (bytes.Length == 0)
        {
            throw new ArgumentException(message: "A buffer frame cannot be empty.", paramName: nameof(bytes));
        }
    }

    private void ValidateBufferFrameStatePinId(BufferFrameState bufferFrameState, ulong? pinId)
    {
        ValidateBufferFrameState(bufferFrameState: bufferFrameState);

        if (pinId == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pinId));
        }
    }

    private void ValidatePinUnderLock(BufferFrameState bufferFrameState, ulong? pinId)
    {
        if (pinId.HasValue && !bufferFrameState.Pins!.Contains(item: pinId.Value))
        {
            throw new ObjectDisposedException(objectName: "PinnedPage");
        }
    }

    private void ValidateBufferFrameStateDirty(BufferFrameState bufferFrameState, ulong pageLogSequenceNumber, ulong? pinId) =>
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

    private void ValidateBufferFrameStateGeneration(BufferFrameState bufferFrameState, ulong dirtyGeneration)
    {
        ValidateBufferFrameState(bufferFrameState: bufferFrameState);

        if (dirtyGeneration > bufferFrameState.DirtyGeneration)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(dirtyGeneration));
        }
    }

    private static void ValidateBufferFrameState(BufferFrameState bufferFrameState)
    {
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState);
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState.Sync);
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState.Bytes);
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState.Pins);
    }

    private static void ValidateCreation(BufferFrameState bufferFrameState)
    {
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState);
        ArgumentNullException.ThrowIfNull(argument: bufferFrameState.Bytes);

        if (bufferFrameState.Bytes.Length == 0)
        {
            throw new ArgumentException(message: "A buffer frame cannot be empty.");
        }
    }
}
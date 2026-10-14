// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Foundations.BufferFrames;
internal sealed partial class BufferFrameService : IBufferFrameService
{
    public BufferFrameState PrepareBufferFrameState(BufferFrameState bufferFrameState) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(bufferFrameState: bufferFrameState);

        return new BufferFrameState
        {
            Sync = new object (),
            PageId = bufferFrameState.PageId,
            Bytes = bufferFrameState.Bytes,
            Pins = new HashSet<ulong>(),
            RecentlyUsed = true
        };
    });

    public ulong ReadBufferFrameStatePageId(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.PageId;
        }
    });

    public int ReadBufferFrameStatePinCount(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.Pins!.Count;
        }
    });

    public bool ReadBufferFrameStateIsDirty(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.IsDirty;
        }
    });

    public bool ReadBufferFrameStateIsEvictable(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.Pins!.Count == 0;
        }
    });

    public ulong ReadBufferFrameStatePageLogSequenceNumber(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.PageLogSequenceNumber;
        }
    });

    public Memory<byte> ReadBufferFrameStateMemory(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            return bufferFrameState.Bytes!;
        }
    });

    public ulong AcquireBufferFrameStatePin(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            var acquiredPinId = checked(bufferFrameState.NextPin + 1);
            bufferFrameState.Pins!.Add(item: acquiredPinId);
            bufferFrameState.NextPin = acquiredPinId;
            bufferFrameState.RecentlyUsed = true;
            return acquiredPinId;
        }
    });

    public void ReleaseBufferFrameStatePin(BufferFrameState bufferFrameState, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStatePinId(bufferFrameState: bufferFrameState, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            if (pinId.HasValue)
            {
                bufferFrameState.Pins!.Remove(item: pinId.Value);
            }
        }
    });

    public void MarkBufferFrameStateDirty(BufferFrameState bufferFrameState, ulong pageLogSequenceNumber, ulong? pinId) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStateDirty(bufferFrameState: bufferFrameState, pageLogSequenceNumber: pageLogSequenceNumber, pinId: pinId);

        lock (bufferFrameState.Sync!)
        {
            ValidatePinUnderLock(bufferFrameState: bufferFrameState, pinId: pinId);
            var nextGeneration = checked(bufferFrameState.DirtyGeneration + 1);
            bufferFrameState.IsDirty = true;
            bufferFrameState.PageLogSequenceNumber = pageLogSequenceNumber;
            bufferFrameState.DirtyGeneration = nextGeneration;
        }
    });

    public (byte[] Bytes, ulong DirtyGeneration, ulong PageLogSequenceNumber) CaptureBufferFrameStateFlushSnapshot(BufferFrameState bufferFrameState) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameState(bufferFrameState: bufferFrameState);

        lock (bufferFrameState.Sync!)
        {
            return (bufferFrameState.Bytes!.ToArray(), bufferFrameState.DirtyGeneration, bufferFrameState.PageLogSequenceNumber);
        }
    });

    public void CompleteBufferFrameStateFlush(BufferFrameState bufferFrameState, ulong dirtyGeneration) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStateGeneration(bufferFrameState: bufferFrameState, dirtyGeneration: dirtyGeneration);

        lock (bufferFrameState.Sync!)
        {
            ValidateBufferFrameStateGeneration(bufferFrameState: bufferFrameState, dirtyGeneration: dirtyGeneration);

            if (bufferFrameState.DirtyGeneration == dirtyGeneration)
            {
                bufferFrameState.IsDirty = false;
            }
        }
    });

    public void ResetBufferFrameState(BufferFrameState bufferFrameState, ulong pageId, byte[] bytes) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameStateReset(bufferFrameState: bufferFrameState, pageId: pageId, bytes: bytes);

        lock (bufferFrameState.Sync!)
        {
            if (bufferFrameState.Pins!.Count != 0)
            {
                throw new InvalidOperationException(message: "A pinned frame cannot be reassigned.");
            }

            bufferFrameState.PageId = pageId;
            bufferFrameState.Bytes = bytes;
            bufferFrameState.IsDirty = false;
            bufferFrameState.PageLogSequenceNumber = 0;
            bufferFrameState.RecentlyUsed = true;
            bufferFrameState.DirtyGeneration = 0;
        }
    });

    public bool GiveBufferFrameStateSecondChance(BufferFrameState bufferFrameState) =>
        TryCatch(operation: () =>
    {
        ValidateBufferFrameState(bufferFrameState: bufferFrameState);

        lock (bufferFrameState.Sync!)
        {
            if (bufferFrameState.Pins!.Count != 0)
            {
                return true;
            }

            if (!bufferFrameState.RecentlyUsed)
            {
                return false;
            }

            bufferFrameState.RecentlyUsed = false;
            return true;
        }
    });
}
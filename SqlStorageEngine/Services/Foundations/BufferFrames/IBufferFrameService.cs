// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Foundations.BufferFrames;
internal interface IBufferFrameService
{
    ulong ReadBufferFrameStatePageId(BufferFrameState bufferFrameState, ulong? pinId);

    int ReadBufferFrameStatePinCount(BufferFrameState bufferFrameState, ulong? pinId);

    bool ReadBufferFrameStateIsDirty(BufferFrameState bufferFrameState, ulong? pinId);

    bool ReadBufferFrameStateIsEvictable(BufferFrameState bufferFrameState, ulong? pinId);

    ulong ReadBufferFrameStatePageLogSequenceNumber(BufferFrameState bufferFrameState, ulong? pinId);

    Memory<byte> ReadBufferFrameStateMemory(BufferFrameState bufferFrameState, ulong? pinId);

    ulong AcquireBufferFrameStatePin(BufferFrameState bufferFrameState, ulong? pinId);

    void ReleaseBufferFrameStatePin(BufferFrameState bufferFrameState, ulong? pinId);

    void MarkBufferFrameStateDirty(BufferFrameState bufferFrameState, ulong pageLogSequenceNumber, ulong? pinId);

    (byte[] Bytes, ulong DirtyGeneration, ulong PageLogSequenceNumber) CaptureBufferFrameStateFlushSnapshot(BufferFrameState bufferFrameState);

    void CompleteBufferFrameStateFlush(BufferFrameState bufferFrameState, ulong dirtyGeneration);

    void ResetBufferFrameState(BufferFrameState bufferFrameState, ulong pageId, byte[] bytes);

    bool GiveBufferFrameStateSecondChance(BufferFrameState bufferFrameState);

    BufferFrameState PrepareBufferFrameState(BufferFrameState bufferFrameState);
}
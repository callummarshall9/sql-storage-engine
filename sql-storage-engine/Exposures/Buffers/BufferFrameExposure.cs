// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Services.Foundations.BufferFrames;

namespace sql_storage_engine.Exposures.Buffers;
public sealed class BufferFrameExposure : sql_storage_engine.Exposures.Buffers.IPinnedPageExposure
{
    private static readonly IBufferFrameService service = new BufferFrameService();
    private readonly BufferFrameState state;
    private readonly ulong? pinId;
    public BufferFrameExposure(PageId pageId, byte[] bytes)
    {
        state = service.PrepareBufferFrameState(bufferFrameState: new BufferFrameState { PageId = pageId.Value, Bytes = bytes });
    }

    internal BufferFrameExposure(BufferFrameState bufferFrameState, ulong? acquiredPinId)
    {
        state = bufferFrameState;
        pinId = acquiredPinId;
    }

    public Memory<byte> ReadMemory() =>
        service.ReadBufferFrameStateMemory(bufferFrameState: state, pinId: pinId);

    public void Dispose() =>
        service.ReleaseBufferFrameStatePin(bufferFrameState: state, pinId: pinId);

    public sql_storage_engine.Exposures.Buffers.IPinnedPageExposure Pin() =>
        new BufferFrameExposure(bufferFrameState: state, acquiredPinId: service.AcquireBufferFrameStatePin(bufferFrameState: state, pinId: pinId));

    public PageId ReadPageId() =>
        new PageId
    {
        Value = service.ReadBufferFrameStatePageId(bufferFrameState: state, pinId: pinId)
    };

    public int ReadPinCount() =>
        service.ReadBufferFrameStatePinCount(bufferFrameState: state, pinId: pinId);

    public bool ReadIsDirty() =>
        service.ReadBufferFrameStateIsDirty(bufferFrameState: state, pinId: pinId);

    public bool ReadIsEvictable() =>
        service.ReadBufferFrameStateIsEvictable(bufferFrameState: state, pinId: pinId);

    public LogSequenceNumber ReadPageLogSequenceNumber() =>
        new LogSequenceNumber
    {
        Value = service.ReadBufferFrameStatePageLogSequenceNumber(bufferFrameState: state, pinId: pinId)
    };

    internal Memory<byte> GetMemory() =>
        service.ReadBufferFrameStateMemory(bufferFrameState: state, pinId: pinId);

    public void MarkDirty(LogSequenceNumber logSequenceNumber) =>
        service.MarkBufferFrameStateDirty(bufferFrameState: state, pageLogSequenceNumber: logSequenceNumber.Value, pinId: pinId);

    internal (byte[] Bytes, ulong DirtyGeneration, LogSequenceNumber PageLogSequenceNumber) CaptureFlushSnapshot()
    {
        var snapshot = service.CaptureBufferFrameStateFlushSnapshot(bufferFrameState: state);
        return (snapshot.Bytes, snapshot.DirtyGeneration, new LogSequenceNumber { Value = snapshot.PageLogSequenceNumber });
    }

    internal void CompleteFlush(ulong dirtyGeneration) =>
        service.CompleteBufferFrameStateFlush(bufferFrameState: state, dirtyGeneration: dirtyGeneration);

    internal void Reset(PageId pageId, byte[] bytes) =>
        service.ResetBufferFrameState(bufferFrameState: state, pageId: pageId.Value, bytes: bytes);

    internal bool GiveSecondChance() =>
        service.GiveBufferFrameStateSecondChance(bufferFrameState: state);
}
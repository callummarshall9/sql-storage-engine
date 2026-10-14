// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Orchestrations.BufferPools;
internal interface IBufferPoolContextOrchestrationService
{
    BufferPoolContext ReadBufferPoolContext();

    BufferPoolContext ReadBufferPoolContextMetrics();

    BufferPoolContext ReadBufferPoolContextCache();

    ValueTask<BufferPoolContext> ReadBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FlushBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FlushBufferPoolContextAsync(CancellationToken cancellationToken);

    ValueTask DiscardBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask DiscardBufferPoolContextAsync(CancellationToken cancellationToken);

    ValueTask CloseBufferPoolContextAsync(BufferPoolContext? bufferPoolContext);

    ulong ReadBufferPoolContextPinPageId(BufferPoolContext bufferPoolContext);

    Memory<byte> ReadBufferPoolContextPinMemory(BufferPoolContext bufferPoolContext);

    ulong ReadBufferPoolContextPinLogSequenceNumber(BufferPoolContext bufferPoolContext);

    bool ReadBufferPoolContextPinIsDirty(BufferPoolContext bufferPoolContext);

    void MarkBufferPoolContextPinDirty(BufferPoolContext bufferPoolContext, ulong logSequenceNumber);

    void ReleaseBufferPoolContextPin(BufferPoolContext bufferPoolContext);

    ValueTask<BufferPoolContext> ReadBufferPoolContextLeaseAsync(ulong pageId, CancellationToken cancellationToken);
}
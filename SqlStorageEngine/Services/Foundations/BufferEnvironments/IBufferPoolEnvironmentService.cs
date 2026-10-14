// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Buffers;

namespace sql_storage_engine.Services.Foundations.BufferEnvironments;
internal interface IBufferPoolEnvironmentService
{
    BufferPoolContext CreateBufferPoolContext(BufferPoolContext bufferPoolContext);

    void EnterBufferPoolContext(BufferPoolContext bufferPoolContext);

    ValueTask EnterBufferPoolContextAsync(BufferPoolContext bufferPoolContext, CancellationToken cancellationToken);

    void ExitBufferPoolContext(BufferPoolContext bufferPoolContext);

    void RecordBufferPoolContextHit(BufferPoolContext bufferPoolContext);

    void RecordBufferPoolContextMiss(BufferPoolContext bufferPoolContext);

    ValueTask ReadBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushBufferPoolContextAsync(BufferPoolContext bufferPoolContext, CancellationToken cancellationToken);

    ValueTask GuardBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken);

    ValueTask CloseBufferPoolContextAsync(BufferPoolContext bufferPoolContext);

    BufferPoolContext ReadBufferPoolContextLifetime(BufferPoolContext bufferPoolContext);
}
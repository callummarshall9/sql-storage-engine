// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.BufferEnvironments;
internal interface IBufferPoolEnvironmentBroker
{
    int ReadPageSize();

    void Enter();

    ValueTask EnterAsync(CancellationToken cancellationToken);

    void Exit();

    void RecordHit();

    void RecordMiss();

    ValueTask ReadAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushAsync(CancellationToken cancellationToken);

    ValueTask GuardAsync(ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken);

    ValueTask CloseAsync();

    IDisposable CreatePinLifetime(Action release);
}
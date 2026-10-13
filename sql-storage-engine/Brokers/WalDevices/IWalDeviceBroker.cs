// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.WalDevices;
internal interface IWalDeviceBroker
{
    long ReadLength();

    ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushAsync(CancellationToken cancellationToken);

    ValueTask RollAsync(ulong segmentNumber, CancellationToken cancellationToken);

    void Truncate(long length);

    Task EnterAsync(CancellationToken cancellationToken);

    void Exit();

    ValueTask PublishWalAppendContextCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken);
}
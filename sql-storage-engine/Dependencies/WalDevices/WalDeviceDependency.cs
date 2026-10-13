// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.WalDevices;
internal sealed class WalDeviceDependency(Func<long> length, Func<long, Memory<byte>, CancellationToken, ValueTask<int>> read, Func<long, ReadOnlyMemory<byte>, CancellationToken, ValueTask<int>> write, Func<CancellationToken, ValueTask> flush, Func<ulong, CancellationToken, ValueTask> roll, Action<long> truncate) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public long ReadLength() =>
        length();

    public ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        read(offset, destination, cancellationToken);

    public ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        write(offset, source, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        flush(cancellationToken);

    public ValueTask RollAsync(ulong segmentNumber, CancellationToken cancellationToken) =>
        roll(segmentNumber, cancellationToken);

    public void Truncate(long length) =>
        truncate(length);

    public Task EnterAsync(CancellationToken cancellationToken) =>
        gate.WaitAsync(cancellationToken);

    public void Exit() =>
        gate.Release();

    public void Dispose() =>
        gate.Dispose();

    public async ValueTask PublishCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await publication(arg1: logSequenceNumber, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }
}
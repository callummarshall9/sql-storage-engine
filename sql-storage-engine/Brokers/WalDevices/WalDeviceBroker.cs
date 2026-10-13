// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Dependencies.WalDevices;

namespace sql_storage_engine.Brokers.WalDevices;
internal sealed class WalDeviceBroker : IWalDeviceBroker
{
    private readonly WalDeviceDependency dependency;
    public WalDeviceBroker(WalDeviceConfiguration walDeviceConfiguration)
    {
        dependency = new WalDeviceDependency(length: walDeviceConfiguration.Length!, read: walDeviceConfiguration.Read!, write: walDeviceConfiguration.Write!, flush: walDeviceConfiguration.Flush!, roll: walDeviceConfiguration.Roll!, truncate: walDeviceConfiguration.Truncate!);
    }

    public long ReadLength() =>
        dependency.ReadLength();

    public ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        dependency.ReadAsync(offset: offset, destination: destination, cancellationToken: cancellationToken);

    public ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteAsync(offset: offset, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        dependency.FlushAsync(cancellationToken: cancellationToken);

    public ValueTask RollAsync(ulong segmentNumber, CancellationToken cancellationToken) =>
        dependency.RollAsync(segmentNumber: segmentNumber, cancellationToken: cancellationToken);

    public void Truncate(long length) =>
        dependency.Truncate(length: length);

    public Task EnterAsync(CancellationToken cancellationToken) =>
        dependency.EnterAsync(cancellationToken: cancellationToken);

    public void Exit() =>
        dependency.Exit();

    public ValueTask PublishWalAppendContextCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        dependency.PublishCheckpointAsync(publication: publication, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);
}
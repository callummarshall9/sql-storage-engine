// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Brokers.WalDevices;

namespace sql_storage_engine.Services.Foundations.WalDevices;
internal sealed partial class WalDeviceService : IWalDeviceService
{
    private readonly WalDeviceBroker broker;
    public WalDeviceService(WalDeviceConfiguration walDeviceConfiguration)
    {
        broker = OpenWalDeviceBroker(walDeviceConfiguration: walDeviceConfiguration);
    }

    private static WalDeviceBroker OpenWalDeviceBroker(WalDeviceConfiguration walDeviceConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(walDeviceConfiguration: walDeviceConfiguration);
        return new WalDeviceBroker(walDeviceConfiguration: walDeviceConfiguration);
    });

    public long ReadWalAppendContextLength() =>
        TryCatch(operation: () =>
    {
        ValidateDevice();
        return broker.ReadLength();
    });

    public ValueTask<int> ReadWalAppendContextAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(offset: offset, destination: destination, cancellationToken: cancellationToken);

        return await broker.ReadAsync(offset: offset, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<int> WriteWalAppendContextAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(offset: offset, source: source, cancellationToken: cancellationToken);

        return await broker.WriteAsync(offset: offset, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushWalAppendContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await broker.FlushAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask RollWalAppendContextAsync(ulong segmentNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRoll(segmentNumber: segmentNumber, cancellationToken: cancellationToken);

        await broker.RollAsync(segmentNumber: segmentNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void TruncateWalAppendContext(long length) =>
        TryCatch(operation: () =>
    {
        ValidateOffset(offset: length);
        broker.Truncate(length: length);
    });

    public ValueTask EnterWalAppendContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await broker.EnterAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitWalAppendContext() =>
        TryCatch(operation: () =>
    {
        ValidateDevice();
        broker.Exit();
    });

    public ValueTask PublishWalAppendContextCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublication(publication: publication, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await broker.PublishWalAppendContextCheckpointAsync(publication: publication, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
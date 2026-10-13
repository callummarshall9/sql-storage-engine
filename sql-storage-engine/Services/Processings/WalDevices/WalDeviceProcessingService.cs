// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalDevices;

namespace sql_storage_engine.Services.Processings.WalDevices;
internal sealed partial class WalDeviceProcessingService(WalDeviceService device) : IWalDeviceProcessingService
{
    internal static WalDeviceProcessingService CreateWalDeviceProcessingService(WalDeviceConfiguration walDeviceConfiguration) =>
        new WalDeviceProcessingService(device: new sql_storage_engine.Services.Foundations.WalDevices.WalDeviceService(walDeviceConfiguration: walDeviceConfiguration));

    public long ReadWalAppendContextLength() =>
        TryCatch(operation: () =>
    {
        ValidateDevice();
        return device.ReadWalAppendContextLength();
    });

    public ValueTask<int> ReadWalAppendContextAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(offset: offset, destination: destination, cancellationToken: cancellationToken);

        return await device.ReadWalAppendContextAsync(offset: offset, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<int> WriteWalAppendContextAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(offset: offset, source: source, cancellationToken: cancellationToken);

        return await device.WriteWalAppendContextAsync(offset: offset, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushWalAppendContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await device.FlushWalAppendContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask RollWalAppendContextAsync(ulong segmentNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRoll(segmentNumber: segmentNumber, cancellationToken: cancellationToken);

        await device.RollWalAppendContextAsync(segmentNumber: segmentNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void TruncateWalAppendContext(long length) =>
        TryCatch(operation: () =>
    {
        ValidateOffset(offset: length);
        device.TruncateWalAppendContext(length: length);
    });

    public ValueTask EnterWalAppendContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await device.EnterWalAppendContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitWalAppendContext() =>
        TryCatch(operation: () =>
    {
        ValidateDevice();
        device.ExitWalAppendContext();
    });

    public ValueTask<byte[]> ReadWalAppendContextBytesAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);
        var length = device.ReadWalAppendContextLength();

        if (length < 0 || length > int.MaxValue)
        {
            throw new sql_storage_engine.Storage.StorageResourceExhaustedException(message: "WAL exceeds the reopen scan bound.");
        }

        var bytes = new byte[checked((int)length)];
        var read = 0;

        while (read < bytes.Length)
        {
            var count = await device.ReadWalAppendContextAsync(offset: read, destination: bytes.AsMemory(start: read), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (count <= 0 || count > bytes.Length - read)
            {
                throw new sql_storage_engine.Storage.StorageFormatException(message: "WAL device returned an invalid reopen read count.");
            }

            read = checked(read + count);
        }

        return bytes;
    });

    public ValueTask WriteWalAppendContextBytesAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(offset: offset, source: source, cancellationToken: cancellationToken);
        var written = 0;

        while (written < source.Length)
        {
            var count = await device.WriteWalAppendContextAsync(offset: checked(offset + written), source: source[written..], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (count <= 0 || count > source.Length - written)
            {
                throw new WalWriteProgressDependencyException();
            }

            written = checked(written + count);
        }
    });

    public ValueTask PublishWalAppendContextCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublication(publication: publication, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await device.PublishWalAppendContextCheckpointAsync(publication: publication, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
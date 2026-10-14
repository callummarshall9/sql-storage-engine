// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Threading;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.WalDevices;
internal sealed partial class WalDeviceService
{
    private static void ValidateConfiguration(WalDeviceConfiguration walDeviceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Length);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Read);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Write);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Flush);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Roll);
        ArgumentNullException.ThrowIfNull(argument: walDeviceConfiguration.Truncate);
    }

    private static void ValidateOffset(long offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: offset);
    }

    private static void ValidateDevice()
    {
    }

    private static void ValidateRead(long offset, Memory<byte> destination, CancellationToken cancellationToken)
    {
        ValidateOffset(offset: offset);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateWrite(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        ValidateOffset(offset: offset);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateCancellation(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRoll(ulong segmentNumber, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value: segmentNumber);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePublication(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: publication);
        ArgumentOutOfRangeException.ThrowIfZero(value: logSequenceNumber);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
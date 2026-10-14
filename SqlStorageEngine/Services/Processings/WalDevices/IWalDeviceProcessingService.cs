// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Processings.WalDevices;
internal interface IWalDeviceProcessingService
{
    long ReadWalAppendContextLength();

    ValueTask<int> ReadWalAppendContextAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask<int> WriteWalAppendContextAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushWalAppendContextAsync(CancellationToken cancellationToken);

    ValueTask RollWalAppendContextAsync(ulong segmentNumber, CancellationToken cancellationToken);

    void TruncateWalAppendContext(long length);

    ValueTask EnterWalAppendContextAsync(CancellationToken cancellationToken);

    void ExitWalAppendContext();

    ValueTask<byte[]> ReadWalAppendContextBytesAsync(CancellationToken cancellationToken);

    ValueTask WriteWalAppendContextBytesAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask PublishWalAppendContextCheckpointAsync(Func<ulong, CancellationToken, ValueTask> publication, ulong logSequenceNumber, CancellationToken cancellationToken);
}
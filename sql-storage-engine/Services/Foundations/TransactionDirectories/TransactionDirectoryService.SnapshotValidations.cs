// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Threading;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    private static void ValidateCaptureSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: snapshotPath);
        ArgumentNullException.ThrowIfNull(argument: headerFactory);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateReadSnapshotBytesAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateHashSnapshotBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(argument: bytes);
    }

    private static void ValidateWriteSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        ArgumentOutOfRangeException.ThrowIfNegative(value: offset);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePublishSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        ArgumentNullException.ThrowIfNull(argument: bytes);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateSnapshotFileLength(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
    }

    private static void ValidateSnapshotRecovery(Func<CancellationToken, ValueTask> operation)
    {
        ArgumentNullException.ThrowIfNull(argument: operation);
    }
}
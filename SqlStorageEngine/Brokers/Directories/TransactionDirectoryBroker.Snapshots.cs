// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.Directories;

namespace sql_storage_engine.Brokers.Directories;
internal sealed partial class TransactionDirectoryBroker
{
    public async ValueTask CaptureSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken)
    {
        using var dependency = new TransactionDirectoryDependency();

        await dependency.CaptureSnapshotAsync(databasePath: databasePath, snapshotPath: snapshotPath, headerFactory: headerFactory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask<byte[]> ReadSnapshotBytesAsync(string path, CancellationToken cancellationToken)
    {
        using var dependency = new TransactionDirectoryDependency();

        return await dependency.ReadSnapshotBytesAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public byte[] HashSnapshotBytes(byte[] bytes)
    {
        using var dependency = new TransactionDirectoryDependency();
        return dependency.HashSnapshotBytes(bytes: bytes);
    }

    public async ValueTask WriteSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken)
    {
        using var dependency = new TransactionDirectoryDependency();

        await dependency.WriteSnapshotStateAsync(path: path, offset: offset, state: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask PublishSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        using var dependency = new TransactionDirectoryDependency();

        await dependency.PublishSnapshotBytesAsync(path: path, bytes: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public long ReadSnapshotFileLength(string path)
    {
        using var dependency = new TransactionDirectoryDependency();
        return dependency.ReadSnapshotFileLength(path: path);
    }

    public async ValueTask RunSnapshotRecoveryAsync(Func<CancellationToken, ValueTask> operation)
    {
        using var dependency = new TransactionDirectoryDependency();

        await dependency.RunSnapshotRecoveryAsync(operation: operation)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}
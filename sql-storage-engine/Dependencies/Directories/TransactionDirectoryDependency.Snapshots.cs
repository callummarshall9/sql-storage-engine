// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.Directories;
internal sealed partial class TransactionDirectoryDependency
{
    public async ValueTask CaptureSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken)
    {
        var temporaryPath = snapshotPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using var source = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var header = headerFactory(source.Length, new byte[32]);

            await destination.WriteAsync(buffer: header, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            using var hash = IncrementalHash.CreateHash(hashAlgorithm: HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            int read;

            while ((read = await source.ReadAsync(buffer: buffer, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)) != 0)
            {
                hash.AppendData(data: buffer, offset: 0, count: read);

                await destination.WriteAsync(buffer: buffer.AsMemory(start: 0, length: read), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            header = headerFactory(source.Length, hash.GetHashAndReset());
            destination.Position = 0;

            await destination.WriteAsync(buffer: header, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            destination.Flush(flushToDisk: true);
            File.Move(sourceFileName: temporaryPath, destFileName: snapshotPath, overwrite: false);
            FlushDirectory(path: snapshotPath);
        }
        finally
        {
            if (File.Exists(path: temporaryPath))
            {
                File.Delete(path: temporaryPath);
            }
        }
    }

    public ValueTask<byte[]> ReadSnapshotBytesAsync(string path, CancellationToken cancellationToken) =>
        new(File.ReadAllBytesAsync(path: path, cancellationToken: cancellationToken));

    public byte[] HashSnapshotBytes(byte[] bytes) =>
        SHA256.HashData(source: bytes);

    public async ValueTask WriteSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        stream.Position = offset;

        await stream.WriteAsync(buffer: new byte[] { state }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        stream.Flush(flushToDisk: true);
    }

    public async ValueTask PublishSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var temporaryPath = path + $".{Guid.NewGuid():N}.recovery";

        try
        {
            await File.WriteAllBytesAsync(path: temporaryPath, bytes: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough))
            {
                stream.Flush(flushToDisk: true);
            }

            File.Move(sourceFileName: temporaryPath, destFileName: path, overwrite: true);
            FlushDirectory(path: path);
        }
        finally
        {
            if (File.Exists(path: temporaryPath))
            {
                File.Delete(path: temporaryPath);
            }
        }
    }

    public long ReadSnapshotFileLength(string path) =>
        new FileInfo(path).Length;

    public async ValueTask RunSnapshotRecoveryAsync(Func<CancellationToken, ValueTask> operation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds: 30));

        await operation(timeout.Token)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}
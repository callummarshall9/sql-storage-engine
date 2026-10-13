// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.Directories;
internal interface ITransactionDirectoryBroker
{
    void DeleteFile(string path);

    void FlushDirectory(string path);

    string GetFileName(string path);

    IReadOnlyList<(string Path, string Name)> GetSiblingFiles(string path);

    bool FileExists(string path);

    ValueTask WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken);

    ValueTask<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken);

    ValueTask CaptureSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken);

    ValueTask<byte[]> ReadSnapshotBytesAsync(string path, CancellationToken cancellationToken);

    byte[] HashSnapshotBytes(byte[] bytes);

    ValueTask WriteSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken);

    ValueTask PublishSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken);

    long ReadSnapshotFileLength(string path);

    ValueTask RunSnapshotRecoveryAsync(Func<CancellationToken, ValueTask> operation);

    string NormalizeFilePath(string path);

    (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareDatabasePublication(string path);

    void PublishDatabaseFile(string temporaryPath, string fullPath);

    ValueTask<byte[]> ReadFilePrefixAsync(string path, int length, CancellationToken cancellationToken);

    System.Guid GenerateIdentity();
}
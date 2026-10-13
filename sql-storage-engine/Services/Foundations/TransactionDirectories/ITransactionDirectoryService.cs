// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal interface ITransactionDirectoryService
{
    void DeleteTransactionFile(string path);

    void FlushTransactionDirectory(string path);

    string GetTransactionFileName(string path);

    IReadOnlyList<(string Path, string Name)> GetSiblingTransactionFiles(string path);

    bool ExistsTransactionFile(string path);

    ValueTask WriteTransactionMetadataAsync<T>(string path, T value, CancellationToken cancellationToken);

    ValueTask<T?> GetTransactionMetadataAsync<T>(string path, CancellationToken cancellationToken);

    ValueTask CaptureTransactionSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken);

    ValueTask<byte[]> ReadTransactionSnapshotBytesAsync(string path, CancellationToken cancellationToken);

    byte[] HashTransactionSnapshotBytes(byte[] bytes);

    ValueTask WriteTransactionSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken);

    ValueTask PublishTransactionSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken);

    long ReadTransactionSnapshotFileLength(string path);

    ValueTask RunTransactionSnapshotRecoveryAsync(Func<CancellationToken, ValueTask> operation);

    string NormalizeTransactionFilePath(string path);

    (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareTransactionDatabasePublication(string path);

    void PublishTransactionDatabaseFile(string temporaryPath, string fullPath);

    ValueTask<byte[]> ReadTransactionFilePrefixAsync(string path, int length, CancellationToken cancellationToken);

    System.Guid GenerateTransactionFileIdentity();
}
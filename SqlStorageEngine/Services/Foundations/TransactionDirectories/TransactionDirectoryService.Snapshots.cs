// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    public ValueTask CaptureTransactionSnapshotAsync(string databasePath, string snapshotPath, Func<long, byte[], byte[]> headerFactory, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCaptureSnapshotAsync(databasePath: databasePath, snapshotPath: snapshotPath, headerFactory: headerFactory, cancellationToken: cancellationToken);

        await transactionDirectoryBroker.CaptureSnapshotAsync(databasePath: databasePath, snapshotPath: snapshotPath, headerFactory: headerFactory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<byte[]> ReadTransactionSnapshotBytesAsync(string path, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReadSnapshotBytesAsync(path: path, cancellationToken: cancellationToken);

        return await transactionDirectoryBroker.ReadSnapshotBytesAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public byte[] HashTransactionSnapshotBytes(byte[] bytes) =>
        TryCatch(operation: () =>
    {
        ValidateHashSnapshotBytes(bytes: bytes);
        return transactionDirectoryBroker.HashSnapshotBytes(bytes: bytes);
    });

    public ValueTask WriteTransactionSnapshotStateAsync(string path, int offset, byte state, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWriteSnapshotStateAsync(path: path, offset: offset, state: state, cancellationToken: cancellationToken);

        await transactionDirectoryBroker.WriteSnapshotStateAsync(path: path, offset: offset, state: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask PublishTransactionSnapshotBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublishSnapshotBytesAsync(path: path, bytes: bytes, cancellationToken: cancellationToken);

        await transactionDirectoryBroker.PublishSnapshotBytesAsync(path: path, bytes: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public long ReadTransactionSnapshotFileLength(string path) =>
        TryCatch(operation: () =>
    {
        ValidateSnapshotFileLength(path: path);
        return transactionDirectoryBroker.ReadSnapshotFileLength(path: path);
    });

    public ValueTask RunTransactionSnapshotRecoveryAsync(Func<CancellationToken, ValueTask> operation) =>
        TryCatch(operation: async () =>
    {
        ValidateSnapshotRecovery(operation: operation);

        await transactionDirectoryBroker.RunSnapshotRecoveryAsync(operation: operation)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
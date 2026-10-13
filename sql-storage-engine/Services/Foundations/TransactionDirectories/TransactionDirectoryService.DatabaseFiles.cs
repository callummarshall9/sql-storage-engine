// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Exceptions;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    public string NormalizeTransactionFilePath(string path) =>
        TryCatch(operation: () =>
    {
        ValidateDatabasePath(path: path);
        return transactionDirectoryBroker.NormalizeFilePath(path: path);
    });

    public (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareTransactionDatabasePublication(string path) =>
        TryCatch(operation: () =>
    {
        ValidateDatabasePath(path: path);
        var fullPath = transactionDirectoryBroker.NormalizeFilePath(path: path);

        if (transactionDirectoryBroker.FileExists(path: fullPath))
        {
            throw new DatabasePublicationConflictException(path: fullPath);
        }

        return transactionDirectoryBroker.PrepareDatabasePublication(path: fullPath);
    });

    public void PublishTransactionDatabaseFile(string temporaryPath, string fullPath) =>
        TryCatch(operation: () =>
    {
        ValidateDatabasePublication(temporaryPath: temporaryPath, fullPath: fullPath);
        transactionDirectoryBroker.PublishDatabaseFile(temporaryPath: temporaryPath, fullPath: fullPath);
    });

    public ValueTask<byte[]> ReadTransactionFilePrefixAsync(string path, int length, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateDatabasePrefix(path: path, length: length, cancellationToken: cancellationToken);

        return await transactionDirectoryBroker.ReadFilePrefixAsync(path: path, length: length, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
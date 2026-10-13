// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

namespace sql_storage_engine.Services.Processings.DatabaseFileProbes;
internal sealed partial class TransactionDirectoryDatabaseFileProcessingService(TransactionDirectoryService directories) : ITransactionDirectoryDatabaseFileProcessingService
{
    public ValueTask<int> ReadTransactionDirectoryDatabasePageSizeAsync(string path, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateProbe(path: path, cancellationToken: cancellationToken);

        var prefix = await directories.ReadTransactionFilePrefixAsync(path: path, length: 64, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (prefix.Length != 64)
        {
            throw new StorageFormatException(message: "Database file is too short to contain a header.");
        }

        if (!prefix.AsSpan(start: 32, length: 8)
            .SequenceEqual(other: "SQLSTORE"u8))
        {
            throw new InvalidDatabaseMagicException();
        }

        var pageSize = BinaryPrimitives.ReadInt32LittleEndian(source: prefix.AsSpan(start: 60, length: 4));

        if (!Enum.IsDefined(value: (PageSize)pageSize))
        {
            throw new InvalidPageSizeException(pageSize: pageSize);
        }

        return pageSize;
    }, path: path);

    public string NormalizeTransactionDirectoryDatabasePath(string path) =>
        TryCatch(operation: () =>
    {
        ValidatePath(path: path);
        return directories.NormalizeTransactionFilePath(path: path);
    });

    public (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareTransactionDirectoryDatabasePublication(string path) =>
        TryCatch(operation: () =>
    {
        ValidatePath(path: path);
        return directories.PrepareTransactionDatabasePublication(path: path);
    });

    public void PublishTransactionDirectoryDatabaseFile(string temporaryPath, string fullPath) =>
        TryCatch(operation: () =>
    {
        ValidatePublication(temporaryPath: temporaryPath, fullPath: fullPath);
        directories.PublishTransactionDatabaseFile(temporaryPath: temporaryPath, fullPath: fullPath);
    });

    public void RemoveTransactionDirectoryDatabaseTemporaryFile(string path) =>
        TryCatch(operation: () =>
    {
        ValidatePath(path: path);

        if (directories.ExistsTransactionFile(path: path))
        {
            directories.DeleteTransactionFile(path: path);
        }
    });

    internal static TransactionDirectoryDatabaseFileProcessingService CreateTransactionDirectoryDatabaseFileProcessingService() =>
        new TransactionDirectoryDatabaseFileProcessingService(directories: sql_storage_engine.Services.Foundations.TransactionDirectories.TransactionDirectoryService.CreateTransactionDirectoryService());
}
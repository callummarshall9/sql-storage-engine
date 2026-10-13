// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Brokers.Directories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService(TransactionDirectoryBroker transactionDirectoryBroker) : ITransactionDirectoryService
{
    public void DeleteTransactionFile(string path) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionFileOnDelete(path: path);
        transactionDirectoryBroker.DeleteFile(path: path);
    });

    public void FlushTransactionDirectory(string path) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionDirectory(path: path);
        transactionDirectoryBroker.FlushDirectory(path: path);
    });

    public string GetTransactionFileName(string path) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionFileNameOnGet(path: path);
        return transactionDirectoryBroker.GetFileName(path: path);
    });

    public IReadOnlyList<(string Path, string Name)> GetSiblingTransactionFiles(string path) =>
        TryCatch(operation: () =>
    {
        ValidateSiblingTransactionFilesOnGet(path: path);
        return transactionDirectoryBroker.GetSiblingFiles(path: path);
    });

    public bool ExistsTransactionFile(string path) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionDirectory(path: path);
        return transactionDirectoryBroker.FileExists(path: path);
    });

    public ValueTask WriteTransactionMetadataAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionMetadata(path: path, value: value, cancellationToken: cancellationToken);

        await transactionDirectoryBroker.WriteJsonAsync(path: path, value: value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<T?> GetTransactionMetadataAsync<T>(string path, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionMetadataOnGet(path: path, cancellationToken: cancellationToken);

        return await transactionDirectoryBroker.ReadJsonAsync<T>(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    internal static TransactionDirectoryService CreateTransactionDirectoryService() =>
        new TransactionDirectoryService(transactionDirectoryBroker: new sql_storage_engine.Brokers.Directories.TransactionDirectoryBroker());

    public System.Guid GenerateTransactionFileIdentity() =>
        TryCatch(operation: () => transactionDirectoryBroker.GenerateIdentity());
}
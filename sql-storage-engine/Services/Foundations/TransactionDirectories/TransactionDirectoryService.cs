// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Models.Transactions;
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

    public ValueTask WriteTransactionMetadataAsync(string path, TransactionMetadata transactionMetadata, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionMetadata(path: path, transactionMetadata: transactionMetadata, cancellationToken: cancellationToken);

        await transactionDirectoryBroker.WriteMetadataBytesAsync(path: path, bytes: EncodeMetadata(transactionMetadata: transactionMetadata), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<TransactionMetadata> ReadTransactionMetadataAsync(string path, bool receipt, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionMetadataOnGet(path: path, receipt: receipt, cancellationToken: cancellationToken);

        var bytes = await transactionDirectoryBroker.ReadMetadataBytesAsync(path: path, length: TransactionMetadataFormat.EncodedLength, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return DecodeMetadata(bytes: bytes, receipt: receipt);
    });

    internal static TransactionDirectoryService CreateTransactionDirectoryService() =>
        new TransactionDirectoryService(transactionDirectoryBroker: new sql_storage_engine.Brokers.Directories.TransactionDirectoryBroker());

    public System.Guid GenerateTransactionFileIdentity() =>
        TryCatch(operation: () => transactionDirectoryBroker.GenerateIdentity());
}
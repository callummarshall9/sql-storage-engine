// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

namespace sql_storage_engine.Services.Processings.TransactionReceipts;
/// <summary>Resolves and publishes transaction receipts while checking identity and decision evidence.</summary>
internal sealed partial class TransactionDirectoryReceiptProcessingService(TransactionDirectoryService transactionDirectoryService) : ITransactionDirectoryReceiptProcessingService
{
    public ValueTask<StorageTransactionReceipt> ResolveStorageTransactionReceiptAsync(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReceiptRequest(path: path, storageTransactionReceipt: storageTransactionReceipt, cancellationToken: cancellationToken);
        var receiptPath = GetReceiptPath(path: path, storageTransactionReceipt: storageTransactionReceipt);

        if (!transactionDirectoryService.ExistsTransactionFile(path: receiptPath))
        {
            return new StorageTransactionReceipt
            {
                Identity = storageTransactionReceipt.Identity,
                State = StorageTransactionState.Indeterminate
            };
        }

        var metadata = await transactionDirectoryService.ReadTransactionMetadataAsync(path: receiptPath, receipt: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var receipt = new StorageTransactionReceipt
        {
            Identity = metadata.Identity,
            State = metadata.State!.Value
        };

        ValidateReceiptContent(storageTransactionReceipt: receipt, expectedStorageTransactionReceipt: storageTransactionReceipt);
        return receipt!;
    });

    public ValueTask WriteStorageTransactionReceiptAsync(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReceiptRequest(path: path, storageTransactionReceipt: storageTransactionReceipt, cancellationToken: cancellationToken);
        ValidateReceiptContent(storageTransactionReceipt: storageTransactionReceipt, expectedStorageTransactionReceipt: storageTransactionReceipt);

        await transactionDirectoryService.WriteTransactionMetadataAsync(path: GetReceiptPath(path: path, storageTransactionReceipt: storageTransactionReceipt), transactionMetadata: new TransactionMetadata { Identity = storageTransactionReceipt.Identity, State = storageTransactionReceipt.State }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ReleaseStorageTransactionReceipt(string path, StorageTransactionReceipt storageTransactionReceipt) =>
        TryCatch(operation: () =>
    {
        ValidateReceiptRequest(path: path, storageTransactionReceipt: storageTransactionReceipt, cancellationToken: CancellationToken.None);
        transactionDirectoryService.DeleteTransactionFile(path: GetReceiptPath(path: path, storageTransactionReceipt: storageTransactionReceipt));
    });

    private static string GetReceiptPath(string path, StorageTransactionReceipt storageTransactionReceipt) =>
        path + $".transaction-{storageTransactionReceipt.Identity.Value:N}.receipt";

    internal static TransactionDirectoryReceiptProcessingService CreateTransactionDirectoryReceiptProcessingService() =>
        new(transactionDirectoryService: sql_storage_engine.Services.Foundations.TransactionDirectories.TransactionDirectoryService.CreateTransactionDirectoryService());
}
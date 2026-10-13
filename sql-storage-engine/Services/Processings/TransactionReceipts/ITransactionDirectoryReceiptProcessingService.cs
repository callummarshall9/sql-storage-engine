// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;

namespace sql_storage_engine.Services.Processings.TransactionReceipts;
internal interface ITransactionDirectoryReceiptProcessingService
{
    ValueTask<StorageTransactionReceipt> ResolveStorageTransactionReceiptAsync(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken cancellationToken);

    ValueTask WriteStorageTransactionReceiptAsync(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken cancellationToken);

    void ReleaseStorageTransactionReceipt(string path, StorageTransactionReceipt storageTransactionReceipt);
}
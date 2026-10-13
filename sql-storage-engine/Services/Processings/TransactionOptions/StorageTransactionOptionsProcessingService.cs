// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models;

namespace sql_storage_engine.Services.Processings.TransactionOptions;
internal sealed partial class StorageTransactionOptionsProcessingService : IStorageTransactionOptionsProcessingService
{
    public StorageTransactionOptions CreateStorageTransactionOptions(TimeSpan timeout, StorageTransactionIsolation isolation, long maximumJournalBytes) =>
        TryCatch(operation: () =>
    {
        ValidateStorageTransactionOptions(timeout: timeout, isolation: isolation, maximumJournalBytes: maximumJournalBytes);

        return new StorageTransactionOptions
        {
            Timeout = timeout,
            Isolation = isolation,
            MaximumJournalBytes = maximumJournalBytes
        };
    });

    public void CheckStorageTransactionOptions(StorageTransactionOptions storageTransactionOptions) =>
        TryCatch(operation: () =>
    {
        ValidateStorageTransactionOptions(storageTransactionOptions: storageTransactionOptions);
    });
}
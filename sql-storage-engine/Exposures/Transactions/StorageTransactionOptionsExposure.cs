// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models;
using sql_storage_engine.Services.Processings.TransactionOptions;

namespace sql_storage_engine.Exposures.Transactions;
/// <summary>Creates and checks transaction options through the transaction-options service.</summary>
public static class StorageTransactionOptionsExposure
{
    private static readonly IStorageTransactionOptionsProcessingService service = new StorageTransactionOptionsProcessingService();
    public static StorageTransactionOptions CreateStorageTransactionOptions(TimeSpan timeout, StorageTransactionIsolation isolation = StorageTransactionIsolation.Serializable, long maximumJournalBytes = StorageTransactionOptions.MaximumSupportedJournalBytes) =>
        service.CreateStorageTransactionOptions(timeout: timeout, isolation: isolation, maximumJournalBytes: maximumJournalBytes);

    public static void CheckStorageTransactionOptions(StorageTransactionOptions storageTransactionOptions) =>
        service.CheckStorageTransactionOptions(storageTransactionOptions: storageTransactionOptions);
}
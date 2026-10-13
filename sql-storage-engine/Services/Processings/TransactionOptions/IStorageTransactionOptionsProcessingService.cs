// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models;

namespace sql_storage_engine.Services.Processings.TransactionOptions;
internal interface IStorageTransactionOptionsProcessingService
{
    StorageTransactionOptions CreateStorageTransactionOptions(TimeSpan timeout, StorageTransactionIsolation isolation, long maximumJournalBytes);

    void CheckStorageTransactionOptions(StorageTransactionOptions storageTransactionOptions);
}
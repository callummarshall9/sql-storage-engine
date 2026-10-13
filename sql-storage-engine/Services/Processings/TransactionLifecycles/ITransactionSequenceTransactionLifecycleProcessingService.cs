// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Processings.TransactionLifecycles;
internal interface ITransactionSequenceTransactionLifecycleProcessingService
{
    TransactionLifecycle CreateTransactionLifecycle(TransactionLifecycle transactionLifecycle);
}
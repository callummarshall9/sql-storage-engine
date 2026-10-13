// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionIdentities;
internal interface ITransactionIdentityService
{
    TransactionLifecycle PrepareTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    ulong ReadTransactionLifecycleId(TransactionLifecycle transactionLifecycle);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionLifecycles;
internal interface ITransactionLifecycleService
{
    TransactionState ReadTransactionLifecycleState(TransactionLifecycle transactionLifecycle);

    void EnsureActiveTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void CommitTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void RollbackTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void CloseTransactionLifecycle(TransactionLifecycle transactionLifecycle);
}
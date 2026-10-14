// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Orchestrations.TransactionLifecycles;
internal interface ITransactionLifecycleOrchestrationService
{
    TransactionLifecycle PrepareTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    ulong ReadTransactionLifecycleId(TransactionLifecycle transactionLifecycle);

    TransactionState ReadTransactionLifecycleState(TransactionLifecycle transactionLifecycle);

    void EnsureActiveTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void CommitTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void RollbackTransactionLifecycle(TransactionLifecycle transactionLifecycle);

    void CloseTransactionLifecycle(TransactionLifecycle transactionLifecycle);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Foundations.TransactionLifecycles;
using sql_storage_engine.Services.Foundations.TransactionIdentities;

namespace sql_storage_engine.Services.Orchestrations.TransactionLifecycles;
internal sealed partial class TransactionLifecycleOrchestrationService(TransactionIdentityService identities, TransactionLifecycleService transitions) : ITransactionLifecycleOrchestrationService
{
    internal static TransactionLifecycleOrchestrationService CreateTransactionLifecycleOrchestrationService() =>
        new TransactionLifecycleOrchestrationService(identities: new TransactionIdentityService(), transitions: new TransactionLifecycleService());

    public TransactionLifecycle PrepareTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        return identities.PrepareTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    });

    public ulong ReadTransactionLifecycleId(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        return identities.ReadTransactionLifecycleId(transactionLifecycle: transactionLifecycle);
    });

    public TransactionState ReadTransactionLifecycleState(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        return transitions.ReadTransactionLifecycleState(transactionLifecycle: transactionLifecycle);
    });

    public void EnsureActiveTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        transitions.EnsureActiveTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    });

    public void CommitTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        transitions.CommitTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    });

    public void RollbackTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        transitions.RollbackTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    });

    public void CloseTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        transitions.CloseTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    });
}
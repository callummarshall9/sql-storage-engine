// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionIdentities;
internal sealed partial class TransactionIdentityService : ITransactionIdentityService
{
    public TransactionLifecycle PrepareTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        return transactionLifecycle;
    });

    public ulong ReadTransactionLifecycleId(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);
        return state.Id;
    });
}
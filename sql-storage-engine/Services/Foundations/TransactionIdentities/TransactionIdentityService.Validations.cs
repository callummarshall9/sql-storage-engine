// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionIdentities;
internal sealed partial class TransactionIdentityService
{
    private static TransactionLifecycleState ValidateTransactionLifecycle(TransactionLifecycle transactionLifecycle)
    {
        ArgumentNullException.ThrowIfNull(argument: transactionLifecycle);
        var state = transactionLifecycle.LifecycleState;

        if (state is null || state.Sync is null || state.Id == 0 || state.Id != transactionLifecycle.TransactionId.Value || transactionLifecycle.Commit is not null || transactionLifecycle.Rollback is not null)
        {
            throw new ArgumentException(message: "The transaction lifecycle is not a valid allocated handle.", paramName: nameof(transactionLifecycle));
        }

        return state;
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Processings.TransactionLifecycles;
internal sealed partial class TransactionSequenceTransactionLifecycleProcessingService
{
    private static void ValidateTransactionLifecycleCreation(TransactionLifecycle transactionLifecycle)
    {
        ArgumentNullException.ThrowIfNull(argument: transactionLifecycle);

        if (transactionLifecycle.TransactionId.Value != 0 || transactionLifecycle.LifecycleState is not null)
        {
            throw new ArgumentException(message: "A transaction allocation request cannot contain an existing identity or lifecycle.", paramName: nameof(transactionLifecycle));
        }
    }
}
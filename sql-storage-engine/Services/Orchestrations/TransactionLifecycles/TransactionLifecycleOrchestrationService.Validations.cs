// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Orchestrations.TransactionLifecycles;
internal sealed partial class TransactionLifecycleOrchestrationService
{
    private static void ValidateTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        Validate(inputs: [(transactionLifecycle is null, nameof(transactionLifecycle))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
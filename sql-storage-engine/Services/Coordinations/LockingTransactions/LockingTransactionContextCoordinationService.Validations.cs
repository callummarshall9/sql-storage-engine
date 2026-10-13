// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Coordinations.LockingTransactions;
internal sealed partial class LockingTransactionContextCoordinationService
{
    private static void ValidateLockingTransactionContext(LockingTransactionContext lockingTransactionContext) =>
        Validate(inputs: [(lockingTransactionContext is null, nameof(lockingTransactionContext)), (lockingTransactionContext?.Lifecycle is null, nameof(LockingTransactionContext.Lifecycle))]);

    private static void ValidateLockingTransactionContext(LockingTransactionContext lockingTransactionContext, CancellationToken cancellationToken)
    {
        ValidateLockingTransactionContext(lockingTransactionContext: lockingTransactionContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

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
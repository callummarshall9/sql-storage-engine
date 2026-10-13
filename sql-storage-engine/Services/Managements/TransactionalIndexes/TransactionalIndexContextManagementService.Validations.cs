// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Managements.TransactionalIndexes;
internal sealed partial class TransactionalIndexContextManagementService
{
    private static void ValidateTransactionalIndexContext(TransactionalIndexContext transactionalIndexContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(transactionalIndexContext is null, nameof(transactionalIndexContext)), (transactionalIndexContext?.Transaction is null, nameof(TransactionalIndexContext.Transaction)), (transactionalIndexContext is not null && !Enum.IsDefined(value: transactionalIndexContext.IsolationLevel), nameof(TransactionalIndexContext.IsolationLevel))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionalIndexContextOnAdd(TransactionalIndexContext newTransactionalIndexContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newTransactionalIndexContext is null, nameof(newTransactionalIndexContext)), (newTransactionalIndexContext?.Transaction is null, nameof(TransactionalIndexContext.Transaction)), (newTransactionalIndexContext?.Key is null, nameof(TransactionalIndexContext.Key))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionalIndexContextOnDelete(TransactionalIndexContext deletedTransactionalIndexContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(deletedTransactionalIndexContext is null, nameof(deletedTransactionalIndexContext)), (deletedTransactionalIndexContext?.Transaction is null, nameof(TransactionalIndexContext.Transaction)), (deletedTransactionalIndexContext?.Key is null, nameof(TransactionalIndexContext.Key))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A transactional index request is required.", paramName: input.ParameterName);
            }
        }
    }
}
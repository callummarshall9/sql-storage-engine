// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Coordinations.IndexTrees;
using sql_storage_engine.Services.Coordinations.LockingTransactions;

namespace sql_storage_engine.Services.Managements.TransactionalIndexes;
internal sealed partial class TransactionalIndexContextManagementService(IIndexTreeContextCoordinationService tree, ILockingTransactionContextCoordinationService locks) : ITransactionalIndexContextManagementService
{
    internal static TransactionalIndexContextManagementService CreateTransactionalIndexContextManagementService(IndexTreeBinding indexTreeBinding, LockingTransactionBinding lockingTransactionBinding) =>
        new TransactionalIndexContextManagementService(tree: (IIndexTreeContextCoordinationService)indexTreeBinding.Service!, locks: (ILockingTransactionContextCoordinationService)lockingTransactionBinding.Service!);

    public ValueTask<TransactionalIndexContext> ReadTransactionalIndexContextAsync(TransactionalIndexContext transactionalIndexContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionalIndexContext(transactionalIndexContext: transactionalIndexContext, cancellationToken: cancellationToken);
        var range = transactionalIndexContext.Range;

        var resource = new IndexRangeLockResource
        {
            IndexId = transactionalIndexContext.IndexId,
            LowerBound = range.LowerBound,
            UpperBound = range.UpperBound,
            IncludeLowerBound = range.LowerBound is not null && range.IncludeLowerBound,
            IncludeUpperBound = range.UpperBound is not null && range.IncludeUpperBound
        };

        await locks.AcquireLockingTransactionContextAsync(lockingTransactionContext: transactionalIndexContext.Transaction!with { Resource = resource, Mode = LockMode.Shared }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var entries = new List<LeafIndexEntry>();

            await foreach (var entry in tree.ReadIndexTreeContextScan(indexTreeContext: new IndexTreeContext { Range = range }, cancellationToken: cancellationToken)
            .Rows!.ConfigureAwait(continueOnCapturedContext: false))
            {
                entries.Add(item: entry);
            }

            transactionalIndexContext.Entries = entries.AsReadOnly();
            return transactionalIndexContext;
        }
        finally
        {
            if (transactionalIndexContext.IsolationLevel != TransactionIsolationLevel.Serializable)
            {
                locks.ReleaseLockingTransactionContext(lockingTransactionContext: transactionalIndexContext.Transaction!with { Resource = resource });
            }
        }
    });

    public ValueTask AddTransactionalIndexContextAsync(TransactionalIndexContext newTransactionalIndexContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionalIndexContextOnAdd(newTransactionalIndexContext: newTransactionalIndexContext, cancellationToken: cancellationToken);

        await locks.AcquireLockingTransactionContextAsync(lockingTransactionContext: newTransactionalIndexContext.Transaction!with { Resource = new IndexKeyLockResource { IndexId = newTransactionalIndexContext.IndexId, Key = newTransactionalIndexContext.Key! }, Mode = LockMode.Exclusive }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await tree.AddIndexTreeContextAsync(newIndexTreeContext: new IndexTreeContext { Key = newTransactionalIndexContext.Key, RowId = newTransactionalIndexContext.RowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<TransactionalIndexContext> DeleteTransactionalIndexContextAsync(TransactionalIndexContext deletedTransactionalIndexContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionalIndexContextOnDelete(deletedTransactionalIndexContext: deletedTransactionalIndexContext, cancellationToken: cancellationToken);

        await locks.AcquireLockingTransactionContextAsync(lockingTransactionContext: deletedTransactionalIndexContext.Transaction!with { Resource = new IndexKeyLockResource { IndexId = deletedTransactionalIndexContext.IndexId, Key = deletedTransactionalIndexContext.Key! }, Mode = LockMode.Exclusive }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        deletedTransactionalIndexContext.DeleteResult = (await tree.DeleteIndexTreeContextAsync(deletedIndexTreeContext: new IndexTreeContext { Key = deletedTransactionalIndexContext.Key, RowId = deletedTransactionalIndexContext.RowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult;

        return deletedTransactionalIndexContext;
    });
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Services.Managements.TransactionalIndexes;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class TransactionalIndexExposure : ICompositionExposure
{
    private readonly TransactionalIndexContextManagementService service;
    private readonly TransactionalIndexContext state;
    public TransactionalIndexExposure(IIndexTreeExposure tree, IndexId indexId, ILockingTransactionExposure transaction, TransactionalIndexOptions transactionalIndexOptions)
    {
        var binding = transaction.ReadLockingTransactionBinding();
        service = TransactionalIndexContextManagementService.CreateTransactionalIndexContextManagementService(indexTreeBinding: tree.ReadIndexTreeBinding(), lockingTransactionBinding: binding);
        state = new TransactionalIndexContext
        {
            IndexId = indexId,
            IsolationLevel = transactionalIndexOptions.IsolationLevel,
            Transaction = binding.State
        };
    }

    public async ValueTask<IReadOnlyList<LeafIndexEntry>> ScanAsync(IndexRange indexRange, CancellationToken cancellationToken = default) =>
        (await service.ReadTransactionalIndexContextAsync(transactionalIndexContext: state with { Range = indexRange }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Entries!;

    public ValueTask InsertAsync(IndexKey newIndexKey, RowId newRowId, CancellationToken cancellationToken = default) =>
        service.AddTransactionalIndexContextAsync(newTransactionalIndexContext: state with { Key = newIndexKey, RowId = newRowId }, cancellationToken: cancellationToken);

    public async ValueTask<IndexDeleteResult> DeleteAsync(IndexKey deletedIndexKey, RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteTransactionalIndexContextAsync(deletedTransactionalIndexContext: state with { Key = deletedIndexKey, RowId = deletedRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult!;
}
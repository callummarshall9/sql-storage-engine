// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Managements.TransactionalIndexes;
internal interface ITransactionalIndexContextManagementService
{
    ValueTask<TransactionalIndexContext> ReadTransactionalIndexContextAsync(TransactionalIndexContext transactionalIndexContext, CancellationToken cancellationToken);

    ValueTask AddTransactionalIndexContextAsync(TransactionalIndexContext newTransactionalIndexContext, CancellationToken cancellationToken);

    ValueTask<TransactionalIndexContext> DeleteTransactionalIndexContextAsync(TransactionalIndexContext deletedTransactionalIndexContext, CancellationToken cancellationToken);
}
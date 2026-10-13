// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Coordinations.IndexTrees;
internal interface IIndexTreeContextCoordinationService
{
    ValueTask<IndexTreeContext> CreateIndexTreeContextAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> AddIndexTreeContextAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> AddIndexTreeContextWithoutSplitAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> DeleteIndexTreeContextAsync(IndexTreeContext deletedIndexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> ReadIndexTreeContextEntriesAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> ReadIndexTreeContextLeafPagesAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken);

    IndexTreeContext ReadIndexTreeContext(IndexTreeContext indexTreeContext);

    IndexTreeContext ReadIndexTreeContextScan(IndexTreeContext indexTreeContext, CancellationToken cancellationToken);

    IndexTreeContext ReadIndexTreeContextBinding(IndexTreeContext indexTreeContext);

    ValueTask<IndexTreeContext> AddIndexTreeContextBindingAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken);

    ValueTask<IndexTreeContext> DeleteIndexTreeContextBindingAsync(IndexTreeContext deletedIndexTreeContext, CancellationToken cancellationToken);
}
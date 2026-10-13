// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Services.Coordinations.IndexTrees;

namespace sql_storage_engine.Exposures.Indexes;
public sealed class PersistentBPlusTreeExposure : ICompositionExposure, IIndexTreeExposure
{
    public const int MaximumTreeHeight = 64;
    public const int MaximumScanPages = 8192;
    private readonly IndexTreeContextCoordinationService service;
    internal PersistentBPlusTreeExposure(IndexTreeBinding indexTreeBinding)
    {
        service = (IndexTreeContextCoordinationService)indexTreeBinding.Service!;
    }

    public PersistentBPlusTreeExposure(IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, IIndexRootReference rootReference, bool isUnique = false)
    {
        service = IndexTreeContextCoordinationService.CreateIndexTreeContextCoordinationService(indexTreeResourceConfiguration: new IndexTreeResourceConfiguration { BufferSource = bufferPool, AllocatorSource = allocator, RootSource = rootReference, PageSize = () => bufferPool.ReadPageSize(), Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token), Allocate = async (type, token) => (await allocator.AllocateAsync(pageType: (PageType)type, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value, ReadRoot = () => rootReference.ReadRootPageId()
            .Value, UpdateRoot = (id, token) => rootReference.UpdateRootAsync(rootPageId: new PageId { Value = id }, cancellationToken: token), IsUnique = isUnique });
    }

    public PageId ReadRootPageId() =>
        service.ReadIndexTreeContext(indexTreeContext: new IndexTreeContext())
            .RootPageId;

    public bool ReadIsUnique() =>
        service.ReadIndexTreeContext(indexTreeContext: new IndexTreeContext())
            .IsUnique;

    public async ValueTask<bool> RemoveAsync(IndexKey indexKey, RowId rowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteIndexTreeContextAsync(deletedIndexTreeContext: new IndexTreeContext { Key = indexKey, RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult!.Removed;

    public async ValueTask<IndexDeleteResult> DeleteAsync(IndexKey deletedIndexKey, RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteIndexTreeContextAsync(deletedIndexTreeContext: new IndexTreeContext { Key = deletedIndexKey, RowId = deletedRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult!;

    public async ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey newIndexKey, RowId newRowId, CancellationToken cancellationToken = default) =>
        (await service.AddIndexTreeContextWithoutSplitAsync(newIndexTreeContext: new IndexTreeContext { Key = newIndexKey, RowId = newRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).InsertResult;

    public async ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default) =>
        (await service.AddIndexTreeContextWithoutSplitAsync(newIndexTreeContext: new IndexTreeContext { Key = newIndexKey, RowId = newRowId, IncludedPayload = includedPayload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).InsertResult;

    public ValueTask InsertAsync(IndexKey newIndexKey, RowId newRowId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.AddIndexTreeContextAsync(newIndexTreeContext: new IndexTreeContext { Key = newIndexKey, RowId = newRowId }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask InsertAsync(IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.AddIndexTreeContextAsync(newIndexTreeContext: new IndexTreeContext { Key = newIndexKey, RowId = newRowId, IncludedPayload = includedPayload }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<IReadOnlyList<RowId>> FindAsync(IndexKey indexKey, CancellationToken cancellationToken = default) =>
        (await service.ReadIndexTreeContextEntriesAsync(indexTreeContext: new IndexTreeContext { Key = indexKey }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Entries!.Select(selector: entry => entry.RowId)
            .ToArray();

    public async ValueTask<IReadOnlyList<LeafIndexEntry>> FindEntriesAsync(IndexKey indexKey, CancellationToken cancellationToken = default) =>
        (await service.ReadIndexTreeContextEntriesAsync(indexTreeContext: new IndexTreeContext { Key = indexKey }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Entries!;

    public IAsyncEnumerable<LeafIndexEntry> ScanAsync(IndexRange indexRange, CancellationToken cancellationToken = default) =>
        service.ReadIndexTreeContextScan(indexTreeContext: new IndexTreeContext { Range = indexRange }, cancellationToken: cancellationToken)
            .Rows!;

    public async ValueTask<IReadOnlyList<PageId>> GetLeafPageIdsAsync(CancellationToken cancellationToken = default) =>
        (await service.ReadIndexTreeContextLeafPagesAsync(indexTreeContext: new IndexTreeContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds!;

    internal IndexTreeContextCoordinationService ReadCoordinationService() =>
        service;

    public IndexTreeBinding ReadIndexTreeBinding() =>
        new IndexTreeBinding
    {
        Service = service
    };
}
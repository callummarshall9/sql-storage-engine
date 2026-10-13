// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Services.Coordinations.IndexTrees;

namespace sql_storage_engine.Exposures.Tables;
public sealed class TableIndexExposure : ICompositionExposure, ITableIndexExposure
{
    private readonly IndexTreeContextCoordinationService service;
    private readonly IndexTreeContext context;
    private readonly IIndexTreeExposure tree;
    public TableIndexExposure(CatalogIndex catalogIndex, IIndexTreeExposure tree)
    {
        service = (IndexTreeContextCoordinationService)tree.ReadIndexTreeBinding()
            .Service!;
        this.tree = tree;
        context = new IndexTreeContext
        {
            Definition = catalogIndex
        };
    }

    public CatalogIndex ReadDefinition() =>
        service.ReadIndexTreeContextBinding(indexTreeContext: context)
            .Definition!;

    public IIndexTreeExposure ReadTree() =>
        tree;

    public int ReadAddCount() =>
        service.ReadIndexTreeContextBinding(indexTreeContext: context)
            .AddCount;

    public int ReadRemoveCount() =>
        service.ReadIndexTreeContextBinding(indexTreeContext: context)
            .RemoveCount;

    internal ValueTask AddAsync(IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken token) =>
        new ValueTask(task: service.AddIndexTreeContextBindingAsync(newIndexTreeContext: context with { Binding = context, Key = newIndexKey, RowId = newRowId, IncludedPayload = includedPayload }, cancellationToken: token)
            .AsTask());

    internal async ValueTask<bool> RemoveAsync(IndexKey indexKey, RowId rowId, CancellationToken token = default) =>
        (await service.DeleteIndexTreeContextBindingAsync(deletedIndexTreeContext: context with { Binding = context, Key = indexKey, RowId = rowId }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult!.Removed;

    public TableIndexBinding ReadTableIndexBinding() =>
        new TableIndexBinding
    {
        Service = service,
        Context = context,
        Definition = context.Definition
    };
}
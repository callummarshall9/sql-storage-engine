// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.IndexRootReferences;

namespace sql_storage_engine.Exposures.Indexes;
public sealed class IndexRootReferenceExposure : IIndexRootReference
{
    private readonly IndexRootReferenceService service;
    public IndexRootReferenceExposure(PageId rootPageId)
    {
        service = new IndexRootReferenceService(configuration: new IndexRootConfiguration { RootPageId = rootPageId.Value });
    }

    public PageId ReadRootPageId() =>
        new PageId
    {
        Value = service.ReadIndexRootPageId()
    };

    public ValueTask UpdateRootAsync(PageId updatedPageId, CancellationToken cancellationToken = default) =>
        service.UpdateIndexRootPageIdAsync(rootPageId: updatedPageId.Value, cancellationToken: cancellationToken);
}
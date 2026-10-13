// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Dependencies.CatalogBuffers;

namespace sql_storage_engine.Brokers.CatalogBuffers;
internal sealed class CatalogBufferBroker : ICatalogBufferBroker
{
    private readonly CatalogBufferDependency dependency;
    public CatalogBufferBroker(CatalogBufferConfiguration catalogBufferConfiguration)
    {
        dependency = new CatalogBufferDependency(flush: catalogBufferConfiguration.Flush!, discard: catalogBufferConfiguration.Discard!);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        dependency.FlushAsync(cancellationToken: cancellationToken);

    public ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.DiscardAsync(pageId: pageId, cancellationToken: cancellationToken);

    public ValueTask EnterAsync(CancellationToken cancellationToken) =>
        dependency.EnterAsync(cancellationToken: cancellationToken);

    public void Exit() =>
        dependency.Exit();
}
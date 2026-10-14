// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.CatalogBuffers;

namespace sql_storage_engine.Services.Foundations.CatalogBuffers;
internal sealed partial class CatalogBufferService : ICatalogBufferService
{
    private readonly CatalogBufferBroker broker;
    private CatalogBufferService(CatalogBufferBroker broker)
    {
        this.broker = broker;
    }

    internal static CatalogBufferService CreateCatalogBufferService(CatalogBufferConfiguration catalogBufferConfiguration) =>
        new CatalogBufferService(broker: new CatalogBufferBroker(catalogBufferConfiguration: catalogBufferConfiguration));

    public ValueTask<CatalogBufferContext> ProcessCatalogBufferContextAsync(CatalogBufferContext catalogBufferContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogBufferContext(catalogBufferContext: catalogBufferContext, cancellationToken: cancellationToken);

        switch (catalogBufferContext.Operation)
        {
            case 1:
                await broker.FlushAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 2:
                await broker.DiscardAsync(pageId: catalogBufferContext.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 3:
                await broker.EnterAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 4:
                broker.Exit();
                break;
        }

        return catalogBufferContext;
    });
}
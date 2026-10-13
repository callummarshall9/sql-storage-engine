// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogStorage;
internal sealed partial class CatalogStorageLifecycleOrchestrationService : ICatalogStorageLifecycleOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.CatalogBuffers.CatalogBufferService buffer;
    private readonly sql_storage_engine.Services.Foundations.CatalogPages.CatalogPageResourceService pages;
    private CatalogStorageLifecycleOrchestrationService(sql_storage_engine.Services.Foundations.CatalogBuffers.CatalogBufferService buffer, sql_storage_engine.Services.Foundations.CatalogPages.CatalogPageResourceService pages)
    {
        this.buffer = buffer;
        this.pages = pages;
    }

    internal static CatalogStorageLifecycleOrchestrationService CreateCatalogStorageLifecycleOrchestrationService(CatalogStorageConfiguration configuration) =>
        new CatalogStorageLifecycleOrchestrationService(buffer: sql_storage_engine.Services.Foundations.CatalogBuffers.CatalogBufferService.CreateCatalogBufferService(catalogBufferConfiguration: configuration.Buffer!), pages: sql_storage_engine.Services.Foundations.CatalogPages.CatalogPageResourceService.CreateCatalogPageResourceService(configuration: configuration.Pages!));

    public ValueTask<CatalogStorageContext> ProcessCatalogStorageContextAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStorageContext(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken);

        switch (catalogStorageContext.Operation)
        {
            case 10:
                await buffer.ProcessCatalogBufferContextAsync(catalogBufferContext: new CatalogBufferContext { Operation = 1 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 11:
                await buffer.ProcessCatalogBufferContextAsync(catalogBufferContext: new CatalogBufferContext { Operation = 2, PageId = catalogStorageContext.RootPageId.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                await pages.FreeCatalogPageAsync(pageId: catalogStorageContext.RootPageId.Value, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 12:
                await buffer.ProcessCatalogBufferContextAsync(catalogBufferContext: new CatalogBufferContext { Operation = 3 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 13:
                await buffer.ProcessCatalogBufferContextAsync(catalogBufferContext: new CatalogBufferContext { Operation = 4 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
        }

        return catalogStorageContext;
    });
}
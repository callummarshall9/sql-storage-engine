// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogStorage;
internal sealed partial class CatalogPublicationCoordinationService : ICatalogPublicationCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.CatalogPages.CatalogPageChainOrchestrationService chains;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogStorage.CatalogStorageLifecycleOrchestrationService lifecycle;
    private CatalogPublicationCoordinationService(sql_storage_engine.Services.Orchestrations.CatalogPages.CatalogPageChainOrchestrationService chains, sql_storage_engine.Services.Orchestrations.CatalogStorage.CatalogStorageLifecycleOrchestrationService lifecycle)
    {
        this.chains = chains;
        this.lifecycle = lifecycle;
    }

    internal static CatalogPublicationCoordinationService CreateCatalogPublicationCoordinationService(CatalogStorageConfiguration configuration) =>
        new CatalogPublicationCoordinationService(chains: sql_storage_engine.Services.Orchestrations.CatalogPages.CatalogPageChainOrchestrationService.CreateCatalogPageChainOrchestrationService(configuration: configuration.Pages!), lifecycle: sql_storage_engine.Services.Orchestrations.CatalogStorage.CatalogStorageLifecycleOrchestrationService.CreateCatalogStorageLifecycleOrchestrationService(configuration: configuration));

    public ValueTask<CatalogStorageContext> ProcessCatalogStorageContextAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStorageContext(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken);

        if (catalogStorageContext.Operation is 8 or 9)
        {
            var result = await chains.ExecuteCatalogPageChainRequestAsync(catalogPageChainRequest: new CatalogPageChainRequest { IsWrite = catalogStorageContext.Operation == 8, Payload = catalogStorageContext.Payload, RootPageId = catalogStorageContext.RootPageId }, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalogStorageContext.Payload = result.Payload;
            catalogStorageContext.RootPageId = result.RootPageId;
            catalogStorageContext.PageIds = result.PageIds;
            return catalogStorageContext;
        }

        return await lifecycle.ProcessCatalogStorageContextAsync(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
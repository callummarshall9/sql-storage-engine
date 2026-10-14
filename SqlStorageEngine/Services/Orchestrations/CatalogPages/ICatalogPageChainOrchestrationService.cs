// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Orchestrations.CatalogPages;
internal interface ICatalogPageChainOrchestrationService
{
    ValueTask<CatalogPageChainRequest> ExecuteCatalogPageChainRequestAsync(CatalogPageChainRequest catalogPageChainRequest, CancellationToken token);
}
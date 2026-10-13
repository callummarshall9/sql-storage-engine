// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal interface ICatalogIndexOptionsOrchestrationService
{
    CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext);
}
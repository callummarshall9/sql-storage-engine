// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments;
internal interface ICatalogIndexDocumentProjectionOrchestrationService
{
    CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext);
}
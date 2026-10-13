// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
internal interface ICatalogIndexShapeOrchestrationService
{
    CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex);
}
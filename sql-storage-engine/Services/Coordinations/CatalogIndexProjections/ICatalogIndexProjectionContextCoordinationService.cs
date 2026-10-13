// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexProjections;
internal interface ICatalogIndexProjectionContextCoordinationService
{
    CatalogIndexProjectionContext ProcessCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext);
}
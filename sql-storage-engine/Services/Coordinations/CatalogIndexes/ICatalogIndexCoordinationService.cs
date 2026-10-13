// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexes;
internal interface ICatalogIndexCoordinationService
{
    CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex);
}
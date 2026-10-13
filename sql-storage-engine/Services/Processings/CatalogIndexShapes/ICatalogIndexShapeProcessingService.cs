// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogIndexShapes;
internal interface ICatalogIndexShapeProcessingService
{
    CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex);
}
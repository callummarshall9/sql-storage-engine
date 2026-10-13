// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.SpecializedIndexOptions;
internal interface ISpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService
{
    CatalogSpecializedIndexOptions PrepareCatalogSpecializedIndexOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions);
}
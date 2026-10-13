// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;
internal interface ICatalogSpecializedIndexOptionsOrchestrationService
{
    CatalogSpecializedIndexOptions PrepareCatalogSpecializedIndexOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions);
}
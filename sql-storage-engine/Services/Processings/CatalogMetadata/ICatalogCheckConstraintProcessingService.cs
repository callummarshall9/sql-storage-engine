// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal interface ICatalogCheckConstraintProcessingService
{
    CatalogCheckConstraint PrepareCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint);
}
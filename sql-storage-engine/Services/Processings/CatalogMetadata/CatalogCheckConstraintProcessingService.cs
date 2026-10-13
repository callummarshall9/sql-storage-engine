// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogCheckConstraintProcessingService : ICatalogCheckConstraintProcessingService
{
    public CatalogCheckConstraint PrepareCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCheckConstraint(catalogCheckConstraint: catalogCheckConstraint);
        return catalogCheckConstraint;
    });
}
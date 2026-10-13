// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogSystemVersioningProcessingService : ICatalogSystemVersioningProcessingService
{
    public CatalogSystemVersioning PrepareCatalogSystemVersioning(CatalogSystemVersioning catalogSystemVersioning) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogSystemVersioning(catalogSystemVersioning: catalogSystemVersioning);
        return catalogSystemVersioning;
    });
}
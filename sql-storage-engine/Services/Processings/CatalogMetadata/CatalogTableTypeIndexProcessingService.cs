// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogTableTypeIndexProcessingService : ICatalogTableTypeIndexProcessingService
{
    public CatalogTableTypeIndex PrepareCatalogTableTypeIndex(CatalogTableTypeIndex catalogTableTypeIndex) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogTableTypeIndex(catalogTableTypeIndex: catalogTableTypeIndex);
        return catalogTableTypeIndex;
    });
}
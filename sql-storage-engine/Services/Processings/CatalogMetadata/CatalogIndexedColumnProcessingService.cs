// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIndexedColumnProcessingService : ICatalogIndexedColumnProcessingService
{
    public CatalogIndexedColumn PrepareCatalogIndexedColumn(CatalogIndexedColumn catalogIndexedColumn) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndexedColumn(catalogIndexedColumn: catalogIndexedColumn);
        return catalogIndexedColumn;
    });
}
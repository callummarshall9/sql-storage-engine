// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogTableProcessingService : ICatalogTableProcessingService
{
    public CatalogTable PrepareCatalogTable(CatalogTable catalogTable) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogTable(catalogTable: catalogTable);
        return catalogTable;
    });
}
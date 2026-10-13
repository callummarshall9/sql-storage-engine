// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIdentityProcessingService : ICatalogIdentityProcessingService
{
    public CatalogIdentity PrepareCatalogIdentity(CatalogIdentity catalogIdentity) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIdentity(catalogIdentity: catalogIdentity);
        return catalogIdentity;
    });
}
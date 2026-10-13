// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogColumnEncryptionProcessingService : ICatalogColumnEncryptionProcessingService
{
    public CatalogColumnEncryption PrepareCatalogColumnEncryption(CatalogColumnEncryption catalogColumnEncryption) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogColumnEncryption(catalogColumnEncryption: catalogColumnEncryption);
        return catalogColumnEncryption;
    });
}
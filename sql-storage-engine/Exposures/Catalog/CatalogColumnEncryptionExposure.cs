// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogColumnEncryptionExposure
{
    private static readonly ICatalogColumnEncryptionProcessingService service = new CatalogColumnEncryptionProcessingService();
    public static CatalogColumnEncryption CreateCatalogColumnEncryption(string keyName, CatalogEncryptionType encryptionType, string algorithm = "AEAD_AES_256_CBC_HMAC_SHA_256") =>
        service.PrepareCatalogColumnEncryption(catalogColumnEncryption: new CatalogColumnEncryption { KeyName = keyName, EncryptionType = encryptionType, Algorithm = algorithm });

    public static CatalogColumnEncryption PrepareCatalogColumnEncryption(CatalogColumnEncryption catalogColumnEncryption) =>
        service.PrepareCatalogColumnEncryption(catalogColumnEncryption: catalogColumnEncryption);
}
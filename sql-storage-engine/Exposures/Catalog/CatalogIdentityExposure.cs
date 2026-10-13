// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogIdentityExposure
{
    private static readonly ICatalogIdentityProcessingService service = new CatalogIdentityProcessingService();
    public static CatalogIdentity CreateCatalogIdentity(long seed = 1, long increment = 1) =>
        service.PrepareCatalogIdentity(catalogIdentity: new CatalogIdentity { Seed = new BigInteger(value: seed), Increment = new BigInteger(value: increment), IsNotForReplication = false });

    public static CatalogIdentity CreateCatalogIdentity(BigInteger seed, BigInteger increment) =>
        service.PrepareCatalogIdentity(catalogIdentity: new CatalogIdentity { Seed = seed, Increment = increment, IsNotForReplication = false });

    public static CatalogIdentity CreateCatalogIdentity(long seed, long increment, bool? isNotForReplication) =>
        service.PrepareCatalogIdentity(catalogIdentity: new CatalogIdentity { Seed = new BigInteger(value: seed), Increment = new BigInteger(value: increment), IsNotForReplication = isNotForReplication });

    public static CatalogIdentity CreateCatalogIdentity(BigInteger seed, BigInteger increment, bool? isNotForReplication) =>
        service.PrepareCatalogIdentity(catalogIdentity: new CatalogIdentity { Seed = seed, Increment = increment, IsNotForReplication = isNotForReplication });

    public static CatalogIdentity PrepareCatalogIdentity(CatalogIdentity catalogIdentity) =>
        service.PrepareCatalogIdentity(catalogIdentity: catalogIdentity);
}
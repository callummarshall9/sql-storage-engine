// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Foundations.CatalogAssemblies;
using sql_storage_engine.Brokers.Catalog;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogAssemblyExposure
{
    private static readonly ICatalogAssemblyService service = new CatalogAssemblyService(metadata: new AssemblyMetadataBroker());
    public static CatalogAssembly CreateCatalogAssembly(string name, ReadOnlySpan<byte> image = default, string? version = null, string? culture = null, string? publicKeyToken = null, CatalogAssemblyPermissionSet permissionSet = CatalogAssemblyPermissionSet.Safe) =>
        service.CreateCatalogAssembly(catalogAssembly: new CatalogAssembly { Name = name, Version = version, Culture = culture, PublicKeyToken = publicKeyToken, PermissionSet = permissionSet }, image: image);

    public static ReadOnlyMemory<byte> ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly) =>
        service.ReadCatalogAssemblyImage(catalogAssembly: catalogAssembly);

    public static CatalogAssembly PrepareCatalogAssembly(CatalogAssembly catalogAssembly) =>
        service.PrepareCatalogAssembly(catalogAssembly: catalogAssembly);
}
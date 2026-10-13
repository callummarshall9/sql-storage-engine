// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class CatalogAssemblyCatalogCodecProcessingService : ICatalogAssemblyCatalogCodecProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService assemblies;
    public CatalogAssemblyCatalogCodecProcessingService(sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService assemblies)
    {
        this.assemblies = assemblies;
    }

    public CatalogAssembly PrepareCatalogAssembly(CatalogAssembly catalogAssembly, byte[] image) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogAssemblyImage(catalogAssembly: catalogAssembly, image: image);
        return assemblies.CreateCatalogAssembly(catalogAssembly: catalogAssembly, image: image);
    });

    public byte[] ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogAssembly(catalogAssembly: catalogAssembly);

        return assemblies.ReadCatalogAssemblyImage(catalogAssembly: catalogAssembly)
            .ToArray();
    });

    internal static CatalogAssemblyCatalogCodecProcessingService CreateCatalogAssemblyCatalogCodecProcessingService() =>
        new CatalogAssemblyCatalogCodecProcessingService(assemblies: sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService.CreateCatalogAssemblyService());
}
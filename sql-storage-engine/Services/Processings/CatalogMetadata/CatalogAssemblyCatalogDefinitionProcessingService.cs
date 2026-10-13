// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogAssemblyCatalogDefinitionProcessingService : ICatalogAssemblyCatalogDefinitionProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService assembliesMetadata;
    public CatalogAssemblyCatalogDefinitionProcessingService(sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService assembliesMetadata)
    {
        this.assembliesMetadata = assembliesMetadata;
    }

    public CatalogDefinition PrepareCatalogDefinition(CatalogDefinition catalogDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogDefinition(catalogDefinition: catalogDefinition);
        return catalogDefinition;
    });

    internal static CatalogAssemblyCatalogDefinitionProcessingService CreateCatalogAssemblyCatalogDefinitionProcessingService() =>
        new CatalogAssemblyCatalogDefinitionProcessingService(assembliesMetadata: sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService.CreateCatalogAssemblyService());
}
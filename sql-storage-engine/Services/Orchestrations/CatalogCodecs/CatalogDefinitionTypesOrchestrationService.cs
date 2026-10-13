// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogDefinitionTypesOrchestrationService : ICatalogDefinitionTypesOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogAssemblyCatalogDefinitionProcessingService definitions;
    private readonly sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogSqlTypeProcessingService sqlTypes;
    private readonly sql_storage_engine.Services.Processings.CatalogCodecs.CatalogAssemblyCatalogCodecProcessingService assemblies;
    public CatalogDefinitionTypesOrchestrationService(sql_storage_engine.Services.Processings.CatalogMetadata.CatalogAssemblyCatalogDefinitionProcessingService definitions, sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogSqlTypeProcessingService sqlTypes, sql_storage_engine.Services.Processings.CatalogCodecs.CatalogAssemblyCatalogCodecProcessingService assemblies)
    {
        this.definitions = definitions;
        this.sqlTypes = sqlTypes;
        this.assemblies = assemblies;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 5:
                catalogCodecContext.PrepareCatalogAssemblyResult = assemblies.PrepareCatalogAssembly(catalogAssembly: catalogCodecContext.CatalogAssembly!, image: catalogCodecContext.Image!);
                return catalogCodecContext;
            case 6:
                catalogCodecContext.ReadCatalogAssemblyImageResult = assemblies.ReadCatalogAssemblyImage(catalogAssembly: catalogCodecContext.CatalogAssembly!);
                return catalogCodecContext;
            case 14:
                catalogCodecContext.ProcessSqlTypeContextRestore73Result = sqlTypes.ProcessSqlTypeContextRestore73(sqlTypeContext: catalogCodecContext.SqlTypeContext!);
                return catalogCodecContext;
            case 15:
                catalogCodecContext.CreateSqlTypeContextCatalogScalarTypeResult = sqlTypes.CreateSqlTypeContextCatalogScalarType(sqlTypeContext: catalogCodecContext.SqlTypeContext!);
                return catalogCodecContext;
            case 18:
                catalogCodecContext.PrepareCatalogDefinitionResult = definitions.PrepareCatalogDefinition(catalogDefinition: catalogCodecContext.CatalogDefinition!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogDefinitionTypesOrchestrationService CreateCatalogDefinitionTypesOrchestrationService() =>
        new CatalogDefinitionTypesOrchestrationService(definitions: sql_storage_engine.Services.Processings.CatalogMetadata.CatalogAssemblyCatalogDefinitionProcessingService.CreateCatalogAssemblyCatalogDefinitionProcessingService(), sqlTypes: sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogSqlTypeProcessingService.CreateSqlCollationCatalogSqlTypeProcessingService(), assemblies: sql_storage_engine.Services.Processings.CatalogCodecs.CatalogAssemblyCatalogCodecProcessingService.CreateCatalogAssemblyCatalogCodecProcessingService());
}
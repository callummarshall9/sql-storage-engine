// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService : IStorageSessionAggregationService
{
    private readonly sql_storage_engine.Services.Managements.CatalogCodecs.CatalogCodecManagementService catalogCodec;
    private readonly sql_storage_engine.Services.Managements.CatalogIndexKeys.CatalogIndexKeyRequestManagementService catalogIndexKeys;
    private readonly sql_storage_engine.Services.Managements.RowCodecs.RowCodecContextManagementService rowCodec;
    private readonly sql_storage_engine.Services.Managements.SqlExpressions.SqlExpressionContextManagementService expressions;
    private readonly sql_storage_engine.Services.Managements.CatalogStorage.CatalogStorageManagementService catalogStorage;
    private readonly sql_storage_engine.Services.Managements.StorageGenerations.StorageGenerationContextManagementService? generations;
    private readonly sql_storage_engine.Services.Managements.SqlValues.SqlValueRequestManagementService sqlValues = sql_storage_engine.Services.Managements.SqlValues.SqlValueRequestManagementService.CreateSqlValueRequestManagementService();
    private readonly sql_storage_engine.Services.Managements.EngineResources.EngineResourceContextManagementService engineResources;
    private readonly sql_storage_engine.Services.Managements.SqlMasks.SqlMaskContextManagementService sqlMasks = new sql_storage_engine.Services.Managements.SqlMasks.SqlMaskContextManagementService();
    private readonly CatalogSessionState catalog;
    private StorageSessionAggregationService(StorageSessionConfiguration configuration)
    {
        engineResources = configuration.EngineResourcesBinding?.Service as sql_storage_engine.Services.Managements.EngineResources.EngineResourceContextManagementService ?? sql_storage_engine.Services.Managements.EngineResources.EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        catalogCodec = sql_storage_engine.Services.Managements.CatalogCodecs.CatalogCodecManagementService.CreateCatalogCodecManagementService();
        catalogIndexKeys = sql_storage_engine.Services.Managements.CatalogIndexKeys.CatalogIndexKeyRequestManagementService.CreateCatalogIndexKeyRequestManagementService();
        rowCodec = configuration.RowCodecBinding?.Service is sql_storage_engine.Services.Managements.RowCodecs.RowCodecContextManagementService boundRows ? boundRows : configuration.Overflow is null ? sql_storage_engine.Services.Managements.RowCodecs.RowCodecContextManagementService.CreateRowCodecContextManagementService(rowEncodingConfiguration: configuration.Rows ?? new RowEncodingConfiguration()) : sql_storage_engine.Services.Managements.RowCodecs.RowCodecContextManagementService.CreateRowCodecContextManagementService(rowEncodingConfiguration: configuration.Rows ?? new RowEncodingConfiguration(), overflowPageResourceConfiguration: configuration.Overflow);
        expressions = sql_storage_engine.Services.Managements.SqlExpressions.SqlExpressionContextManagementService.CreateSqlExpressionContextManagementService();
        catalogStorage = configuration.CatalogStorage is null ? null! : sql_storage_engine.Services.Managements.CatalogStorage.CatalogStorageManagementService.CreateCatalogStorageManagementService(configuration: configuration.CatalogStorage);
        if (configuration.Generation?.DatabaseBinding is not null)
        {
            generations = sql_storage_engine.Services.Managements.StorageGenerations.StorageGenerationContextManagementService.CreateStorageGenerationContextManagementService(pageDatabaseBinding: configuration.Generation.DatabaseBinding, timeProviderSource: configuration.Generation.TimeProviderSource);
        }

        catalog = new CatalogSessionState
        {
            Definition = CreateCatalogDefinition(tables: [], indexes: []),
            HeapBindings = new(),
            IndexBindings = new(),
            IndexRoots = new(),
            TableStorages = new()
        };
    }

    internal static StorageSessionAggregationService CreateStorageSessionAggregationService(StorageSessionConfiguration configuration) =>
        new StorageSessionAggregationService(configuration: configuration);

    public StorageSessionContext ReadStorageSessionContextCatalog(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageSessionContext(storageSessionContext: storageSessionContext);

        storageSessionContext.Binding = new StorageSessionBinding
        {
            Service = this
        };

        storageSessionContext.Catalog!.Definition = catalog.Definition;
        storageSessionContext.Catalog.RootPageId = catalog.RootPageId;
        return storageSessionContext;
    });
    private readonly sql_storage_engine.Services.Managements.SecuritySessions.SecuritySessionManagementService securitySessions = sql_storage_engine.Services.Managements.SecuritySessions.SecuritySessionManagementService.CreateSecuritySessionManagementService();
}
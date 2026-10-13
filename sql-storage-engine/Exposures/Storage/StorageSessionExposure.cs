// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Services.Aggregations.StorageSessions;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure : IStorageTable, IStorageIndex, ICompositionExposure, System.IAsyncDisposable, IStorageEngine, IStorageTransaction, IStorageTransactionContext
{
    private readonly StorageSessionAggregationService service;
    private readonly StoragePublicTableContext tableContext;
    private readonly StorageIndexContext indexContext;
    private readonly StorageSessionContext sessionContext;
    private readonly StorageEngineRuntime engineContext;
    public StorageSessionExposure(StorageSessionBinding storageSessionBinding, StorageSessionContext storageSessionContext)
    {
        service = (StorageSessionAggregationService)storageSessionBinding.Service!;
        sessionContext = storageSessionContext;
        engineContext = storageSessionContext.Engine ?? storageSessionBinding.Engine!;
        tableContext = storageSessionContext.PublicTable!;
        indexContext = storageSessionContext.Index!;
    }

    /// <summary>Binds a view to the service and runtime carried by a completed request.</summary>
    private StorageSessionExposure(StorageSessionContext storageSessionContext) : this(storageSessionBinding: storageSessionContext.Binding!, storageSessionContext: storageSessionContext)
    {
    }

    /// <summary>Composes the initial terminal used to acquire engine resources.</summary>
    private StorageSessionExposure(StorageSessionConfiguration configuration)
    {
        service = StorageSessionAggregationService.CreateStorageSessionAggregationService(configuration: configuration);
        sessionContext = null!;
        tableContext = null!;
        indexContext = null!;
        engineContext = null!;
    }
}
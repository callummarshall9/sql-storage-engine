// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    /// <summary>Creates one configured session view from the shared terminal binding.</summary>
    private async ValueTask<StorageSessionExposure> CreateStorageSessionExposureCoreAsync(string path, StorageEngineOptions? storageEngineOptions, bool creating, CancellationToken cancellationToken) =>
        new StorageSessionExposure(storageSessionContext: await service.CreateStorageSessionContextEngineAsync(storageSessionContext: new StorageSessionContext { Creation = new StorageEngineCreationContext { Path = path, Options = storageEngineOptions, Creating = creating } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public static ValueTask<StorageSessionExposure> CreateStorageSessionExposureAsync(string path, StorageEngineOptions? storageEngineOptions = null, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(configuration: new StorageSessionConfiguration()).CreateStorageSessionExposureCoreAsync(path: path, storageEngineOptions: storageEngineOptions, creating: true, cancellationToken: cancellationToken);

    public static ValueTask<StorageSessionExposure> OpenStorageSessionExposureAsync(string path, StorageEngineOptions? storageEngineOptions = null, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(configuration: new StorageSessionConfiguration()).CreateStorageSessionExposureCoreAsync(path: path, storageEngineOptions: storageEngineOptions, creating: false, cancellationToken: cancellationToken);

    public async ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(storageSessionContext: (await service.ReadStorageSessionContextEngineHandleAsync(storageSessionContext: new StorageSessionContext { OpenHandle = new StorageEngineOpenHandleContext { TableId = tableId, Handle = new StorageHandleContext { Engine = engineContext, Generation = engineContext.Generation, Statement = sessionContext.Statement } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).OpenHandle!.Result!);

    public async ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(storageSessionContext: (await service.ReadStorageSessionContextEngineHandleAsync(storageSessionContext: new StorageSessionContext { OpenHandle = new StorageEngineOpenHandleContext { IndexId = indexId, Handle = new StorageHandleContext { Engine = engineContext, Generation = engineContext.Generation, Statement = sessionContext.Statement } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).OpenHandle!.Result!);
}
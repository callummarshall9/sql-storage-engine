// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Access;

namespace sql_storage_engine.Services.Foundations.StorageAccesses;
internal interface IStorageAccessService
{
    StorageAccess? ReadCurrentStorageAccess();

    ValueTask<StorageAccess> EnterStorageAccessAsync(StorageAccess? storageAccess, CancellationToken token, bool reentrant);

    StorageAccess EnterImmediateStorageAccess(StorageAccess? storageAccess);

    StorageAccess ActivateStorageAccess(StorageAccess storageAccess);

    void RegisterStorageAccessResource(StorageAccess storageAccess, IAsyncDisposable resource);

    Task DrainStorageAccessAsync(StorageAccess storageAccess);

    void CloseStorageAccess(StorageAccess? storageAccess);

    IDisposable ReadStorageAccessLifetime(StorageAccess storageAccess);
}
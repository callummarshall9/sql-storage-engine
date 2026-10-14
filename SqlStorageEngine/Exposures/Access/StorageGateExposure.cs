// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Access;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.StorageAccesses;

namespace sql_storage_engine.Exposures.Access;
public sealed class StorageGateExposure : IDisposable
{
    private readonly StorageAccessService service;
    private readonly StorageAccess? access;
    public StorageGateExposure()
    {
        service = new StorageAccessService();
    }

    private StorageGateExposure(StorageAccessService service, StorageAccessConfiguration configuration)
    {
        this.service = service;
        access = configuration.Access;
    }

    public StorageAccess? ReadCurrent() =>
        service.ReadCurrentStorageAccess();

    public async ValueTask<StorageGateExposure> EnterAsync(CancellationToken token, bool reentrant = true) =>
        new StorageGateExposure(service: service, configuration: new StorageAccessConfiguration { Access = await service.EnterStorageAccessAsync(storageAccess: access, token: token, reentrant: reentrant)
            .ConfigureAwait(continueOnCapturedContext: false) });

    public StorageGateExposure EnterImmediate() =>
        new StorageGateExposure(service: service, configuration: new StorageAccessConfiguration { Access = service.EnterImmediateStorageAccess(storageAccess: access) });

    public StorageGateExposure Activate() =>
        new StorageGateExposure(service: service, configuration: new StorageAccessConfiguration { Access = service.ActivateStorageAccess(storageAccess: access!) });

    public void RegisterScope(StorageAccess storageAccess, IAsyncDisposable resource) =>
        service.RegisterStorageAccessResource(storageAccess: storageAccess, resource: resource);

    public Task DrainAsync() =>
        service.DrainStorageAccessAsync(storageAccess: access!);

    public void Dispose() =>
        service.CloseStorageAccess(storageAccess: access);
}
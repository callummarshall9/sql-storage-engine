// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Orchestrations.EngineAccesses;
internal sealed partial class EngineAccessResourceOrchestrationService : IEngineAccessResourceOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.TransactionRuntimes.TransactionRuntimeService runtime = new();
    private readonly sql_storage_engine.Services.Foundations.StorageAccesses.StorageAccessService access;
    private sql_storage_engine.Services.Foundations.FileLeases.StorageFileLeaseService? files;
    private EngineAccessResourceOrchestrationService(sql_storage_engine.Services.Foundations.StorageAccesses.StorageAccessService access, sql_storage_engine.Services.Foundations.FileLeases.StorageFileLeaseService files)
    {
        this.access = access;
        this.files = files;
    }

    internal static EngineAccessResourceOrchestrationService CreateEngineAccessResourceOrchestrationService() =>
        new(access: new sql_storage_engine.Services.Foundations.StorageAccesses.StorageAccessService(), files: null!);

    public EngineResourceContext AcquireEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        files = new sql_storage_engine.Services.Foundations.FileLeases.StorageFileLeaseService(configuration: new StorageFileLeaseConfiguration { Path = engineResourceContext.Path, Creating = engineResourceContext.Creating });
        return engineResourceContext;
    });

    public EngineResourceContext CloseEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        files?.CloseStorageFileLease();
        return engineResourceContext;
    });

    public EngineResourceContext ReadEngineResourceContextAccess(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);

        return engineResourceContext with
        {
            Access = access.ReadCurrentStorageAccess()
        };
    });

    public ValueTask<EngineResourceContext> EnterEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        var entered = await access.EnterStorageAccessAsync(storageAccess: engineResourceContext.Access, token: cancellationToken, reentrant: engineResourceContext.Reentrant)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext with
        {
            Access = entered,
            Lifetime = access.ReadStorageAccessLifetime(storageAccess: entered)
        };
    });

    public EngineResourceContext EnterImmediateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        var entered = access.EnterImmediateStorageAccess(storageAccess: engineResourceContext.Access);

        return engineResourceContext with
        {
            Access = entered,
            Lifetime = access.ReadStorageAccessLifetime(storageAccess: entered)
        };
    });

    public EngineResourceContext ActivateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        var active = access.ActivateStorageAccess(storageAccess: engineResourceContext.Access!);

        return engineResourceContext with
        {
            Access = active,
            Lifetime = access.ReadStorageAccessLifetime(storageAccess: active)
        };
    });

    public EngineResourceContext ReleaseEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        access.CloseStorageAccess(storageAccess: engineResourceContext.Access);
        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> DrainEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await access.DrainStorageAccessAsync(storageAccess: engineResourceContext.Access!)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public EngineResourceContext RegisterScopeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        access.RegisterStorageAccessResource(storageAccess: engineResourceContext.Access!, resource: engineResourceContext.ScopeResource!);
        return engineResourceContext;
    });

}
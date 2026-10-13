// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Security;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextSecurityAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch<StorageSessionContext>(operation: async () =>
    {
        ValidateStorageSessionContextSecurity(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var engine = storageSessionContext.Engine!;
        var request = storageSessionContext.SecurityRequest!;

        if (request.Operation is 8 or 9)
        {
            await ExecuteSecurityEventCoreAsync(engine: engine, request: request, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return storageSessionContext;
        }

        if (request.Operation == 10)
        {
            if (engineResources.ReadEngineResourceContextAccess(engineResourceContext: new EngineResourceContext())
            .Access is not null)
            {
                throw new InvalidOperationException(message: "Use the callback security snapshot.");
            }

            var lease = await EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = engine, Generation = engine.Generation } }, cancellationToken: cancellationToken, reentrant: false)
            .ConfigureAwait(continueOnCapturedContext: false);

            using (lease.Lifetime!)
            {
                request.SnapshotResult = (await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = 0, State = catalog.Definition.Security }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Snapshot;
            }

            return storageSessionContext;
        }

        CheckSecurityCallback(storageSessionContext: storageSessionContext);

        if (request.Operation == 0)
        {
            request.SnapshotResult = (await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = 0, State = catalog.Definition.Security }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Snapshot;

            return storageSessionContext;
        }

        var entered = await EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = engine, Generation = engine.Generation, Statement = storageSessionContext.Statement } }, cancellationToken: cancellationToken, reentrant: true)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = entered.Lifetime!;
        var active = engineResources.ActivateEngineResourceContext(engineResourceContext: entered);
        using var activation = active.Lifetime!;
        CheckSecurityCallback(storageSessionContext: storageSessionContext);

        try
        {
            if (request.Operation == 6)
            {
                var admitted = (await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = 6, Admission = new SecurityAdmission { Request = request.Authorization, State = catalog.Definition.Security, Tables = catalog.Definition.Tables, DatabaseId = ((EngineResourceContext)engine.ResourceSource!).Header!.DatabaseId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Admission!;

                if (admitted.ReadOnly)
                {
                    await ExecuteSecurityEventCoreAsync(engine: engine, request: new StorageSecurityRequestContext { Operation = 8, Record = admitted.Record }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                }

                await request.Callback!(arg1: storageSessionContext, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await engineResources.DrainEngineResourceContextAsync(engineResourceContext: active, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

                CheckSecurityCallback(storageSessionContext: storageSessionContext);
                cancellationToken.ThrowIfCancellationRequested();

                if (catalog.Definition.Security != admitted.State)
                {
                    throw new StorageAuthorizationException(reason: "Security changed inside an admitted operation.");
                }

                if (!admitted.ReadOnly)
                {
                    await PublishSecurityMutationCoreAsync(engine: engine, state: admitted.State!, record: admitted.Record!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
            }
            else
            {
                var mutation = request.Mutation!with
                {
                    State = catalog.Definition.Security,
                    Tables = catalog.Definition.Tables,
                    DatabaseId = ((EngineResourceContext)engine.ResourceSource!).Header!.DatabaseId
                };

                var prepared = (await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = request.Operation, Mutation = mutation }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Mutation!;

                await PublishSecurityMutationCoreAsync(engine: engine, state: prepared.State!, record: prepared.Record!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch
        {
            storageSessionContext.Statement!.DdlFailed = true;
            throw;
        }

        return storageSessionContext;
    });

    public StorageSessionContext ReadStorageSessionContextSecurityObject(StorageSessionContext storageSessionContext) =>
        TryCatch<StorageSessionContext>(operation: () =>
    {
        ValidateStorageSessionContextSecurity(storageSessionContext: storageSessionContext, cancellationToken: CancellationToken.None);
        CheckSecurityCallback(storageSessionContext: storageSessionContext);

        if (catalog.Definition.Security.Revision == 1)
        {
            throw new InvalidOperationException(message: "Provision a principal before resolving security bindings.");
        }

        var table = catalog.Definition.Tables.FirstOrDefault(predicate: item => item.Id == storageSessionContext.SecurityRequest!.TableId) ?? throw new ArgumentException(message: "Unknown object.");

        storageSessionContext.SecurityRequest!.ObjectResult = new StorageObjectBinding
        {
            DatabaseId = ((EngineResourceContext)storageSessionContext.Engine!.ResourceSource!).Header!.DatabaseId,
            ObjectId = table.ObjectId,
            SchemaVersion = table.SchemaVersion
        };

        return storageSessionContext;
    });

    private static void CheckSecurityCallback(StorageSessionContext storageSessionContext)
    {
        if (storageSessionContext.Statement?.Active != true)
        {
            throw new InvalidOperationException(message: "The transaction callback has ended.");
        }

        if (storageSessionContext.Engine!.Disposed)
        {
            throw new ObjectDisposedException(objectName: "StorageSessionExposure");
        }

        if (storageSessionContext.Engine.ActiveTransaction is null || storageSessionContext.Statement.Transaction is null)
        {
            throw new InvalidOperationException(message: "Security catalog access requires an explicit transaction callback.");
        }
    }

    private async ValueTask PublishSecurityMutationCoreAsync(StorageEngineRuntime engine, StorageSecurityState state, StorageAuditRecord record, CancellationToken cancellationToken)
    {
        await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = 7, Record = record }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (state.Audit.Length >= 4096)
        {
            throw new InvalidOperationException(message: "Mutation audit quota exceeded.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        engine.TransactionObserver?.Invoke(obj: "security-mutation-before-audit");

        await WriteSecurityAsync(state: state with { Audit = [..state.Audit, record] }, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        engine.TransactionObserver?.Invoke(obj: "security-mutation-after-audit");
    }

    private async ValueTask ExecuteSecurityEventCoreAsync(StorageEngineRuntime engine, StorageSecurityRequestContext request, CancellationToken cancellationToken)
    {
        if (engine.Disposed)
        {
            throw new ObjectDisposedException(objectName: "StorageSessionExposure");
        }

        var runtime = engineResources.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: new EngineResourceContext { Timeout = TimeSpan.FromSeconds(seconds: 30), ExpiryNotification = () =>
        {
        } });

        try
        {
            engineResources.StartTransactionRuntimeEngineResourceContext(engineResourceContext: runtime);
            var linked = engineResources.LinkTransactionTokenEngineResourceContext(engineResourceContext: runtime with { InputToken = cancellationToken });
            using var linkedLifetime = linked.Lifetime!;

            var entered = await engineResources.EnterSecurityEngineResourceContextAsync(engineResourceContext: new EngineResourceContext(), cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            using var gate = entered.SecurityLifetime!;

            if (engine.Disposed)
            {
                throw new ObjectDisposedException(objectName: "StorageSessionExposure");
            }

            var resources = (EngineResourceContext)engine.ResourceSource!;

            var events = new SecurityEventContext
            {
                TransactionPath = resources.Path,
                DatabaseId = resources.Header!.DatabaseId.Value,
                HasTransaction = engine.ActiveTransaction is not null,
                Uncertain = engine.SecurityEventsUncertain,
                Observer = engine.TransactionObserver,
                Record = request.Record
            };

            try
            {
                var result = await securitySessions.ProcessSecuritySessionContextAsync(securitySessionContext: new SecuritySessionContext { Operation = request.Operation, Events = events }, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

                request.EventsResult = result.Events!.Records;
            }
            finally
            {
                engine.SecurityEventsUncertain = events.Uncertain;
            }
        }
        finally
        {
            engineResources.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: runtime);
        }
    }

    private static void ValidateStorageSessionContextSecurity(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Engine);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.SecurityRequest);
        cancellationToken.ThrowIfCancellationRequested();

        if (storageSessionContext.SecurityRequest.Operation == 6)
        {
            ArgumentNullException.ThrowIfNull(argument: storageSessionContext.SecurityRequest.Authorization);
            ArgumentNullException.ThrowIfNull(argument: storageSessionContext.SecurityRequest.CallbackSource);
        }
    }

    public StorageSessionContext ReadStorageSessionContextSecuritySnapshot(StorageSessionContext storageSessionContext) =>
        TryCatch<StorageSessionContext>(operation: () =>
    {
        ValidateStorageSessionContextSecurity(storageSessionContext: storageSessionContext, cancellationToken: CancellationToken.None);
        CheckSecurityCallback(storageSessionContext: storageSessionContext);

        storageSessionContext.SecurityRequest!.SnapshotResult = securitySessions.ReadSecuritySessionContextSnapshot(securitySessionContext: new sql_storage_engine.Models.Security.SecuritySessionContext { State = catalog.Definition.Security })
            .Snapshot;

        return storageSessionContext;
    });
}
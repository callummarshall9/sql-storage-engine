// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextEngineLifecycleAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextEngineLifecycle(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var runtime = storageSessionContext.Engine!;
        var request = storageSessionContext.Execution!;
        var resources = (EngineResourceContext)runtime.ResourceSource!;

        switch (request.Operation)
        {
            case "Statement":
                await ExecutePublicStatementCoreAsync(storageEngineRuntime: runtime, operation: request.OperationCallback!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Flush":
                var lease = await EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = runtime, Generation = runtime.Generation } }, cancellationToken: cancellationToken, reentrant: true)
            .ConfigureAwait(continueOnCapturedContext: false);
                using (lease.Lifetime!)
                {
                    using (engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!)
                    {
                        await engineResources.FlushBufferEngineResourceContextAsync(engineResourceContext: resources, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                }

                break;
            case "Dispose":
                if (storageSessionContext.Transaction is not null)
                {
                    await DisposePublicTransactionAsync(storagePublicTransactionContext: storageSessionContext.Transaction)
            .ConfigureAwait(continueOnCapturedContext: false);

                    break;
                }

                await DisposePublicEngineAsync(storageEngineRuntime: runtime)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentException(message: "Unknown engine lifecycle operation.", paramName: nameof(StorageEngineExecutionContext.Operation));
        }

        return storageSessionContext;
    });

    private async ValueTask ExecutePublicStatementCoreAsync(StorageEngineRuntime storageEngineRuntime, Func<StorageSessionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken, StoragePublicTransactionContext? transaction = null)
    {
        var resource = (EngineResourceContext)storageEngineRuntime.ResourceSource!;

        var lease = await EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = storageEngineRuntime, Generation = storageEngineRuntime.Generation } }, cancellationToken: cancellationToken, reentrant: true)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = lease.Lifetime!;
        var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease);
        using var scopeLifetime = scope.Lifetime!;
        EngineResourceContext? journal = null;

        var statement = new StorageStatementRuntime
        {
            Active = true,
            Transaction = transaction
        };

        try
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storageEngineRuntime, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            journal = await engineResources.CreateJournalEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                await operation(arg1: new StorageSessionContext { Binding = new StorageSessionBinding { Service = this, Engine = storageEngineRuntime }, Engine = storageEngineRuntime, Statement = statement, Transaction = transaction }, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            finally
            {
                statement.Active = false;

                await engineResources.DrainEngineResourceContextAsync(engineResourceContext: scope, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            if (statement.DdlFailed)
            {
                throw new InvalidOperationException(message: "The statement contains a failed DDL operation.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await FlushPublicEngineAsync(storageEngineRuntime: storageEngineRuntime, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await engineResources.CommitJournalEngineResourceContextAsync(engineResourceContext: journal, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception failure)
        {
            statement.Active = false;

            if (journal is not null)
            {
                try
                {
                    await RestorePublicEngineJournalAsync(storageEngineRuntime: storageEngineRuntime, journal: journal, retain: false)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(message: "Statement failed and its durable rollback also failed.", innerExceptions: [failure, rollbackFailure]);
                }
            }

            throw;
        }
        finally
        {
            if (journal is not null)
            {
                await engineResources.CloseJournalEngineResourceContextAsync(engineResourceContext: journal, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }

    private async ValueTask RestorePublicEngineJournalAsync(StorageEngineRuntime storageEngineRuntime, EngineResourceContext journal, bool retain, CancellationToken cancellationToken = default)
    {
        var resource = (EngineResourceContext)storageEngineRuntime.ResourceSource!;

        await engineResources.DiscardBufferEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var restored = await engineResources.RestoreJournalEngineResourceContextAsync(engineResourceContext: journal with { Retain = retain }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        catalogStorage.ResetCatalogStorageContext(catalogStorageContext: new CatalogStorageContext());
        heapIndexMutations.Clear();
        catalog.TableStorages!.Clear();
        catalog.HeapBindings!.Clear();
        catalog.IndexBindings!.Clear();
        catalog.IndexRoots!.Clear();

        if ((await engineResources.ReloadHeaderEngineResourceContextAsync(engineResourceContext: restored, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Header!.CatalogRootPageId is { } root)
        {
            await ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 100, RootPageId = root } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            catalog.Definition = CreateCatalogDefinition(tables: [], indexes: []);
            catalog.RootPageId = null;
        }

        storageEngineRuntime.Generation++;
    }

    private async ValueTask DisposePublicEngineAsync(StorageEngineRuntime storageEngineRuntime)
    {
        if (storageEngineRuntime.Disposed)
        {
            return;
        }

        var resource = (EngineResourceContext)storageEngineRuntime.ResourceSource!;

        if (engineResources.ReadEngineResourceContextAccess(engineResourceContext: resource)
            .Access is not null)
        {
            throw new InvalidOperationException(message: "Dispose storage after its callback returns.");
        }

        if (storageEngineRuntime.PendingCleanup is { IsCompleted: false })
        {
            throw new InvalidOperationException(message: "Transaction cleanup is still running; retry disposal after the callback stops.");
        }

        if (storageEngineRuntime.ActiveTransaction is { } transaction)
        {
            try
            {
                await DisposePublicTransactionAsync(storagePublicTransactionContext: transaction)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch
            {
                storageEngineRuntime.Quarantined = true;
            }
        }

        if (storageEngineRuntime.PendingCleanup is { IsCompleted: false })
        {
            throw new InvalidOperationException(message: "Transaction cleanup is still running; retry disposal after the callback stops.");
        }

        var entered = await engineResources.EnterEngineResourceContextAsync(engineResourceContext: resource with { Reentrant = false }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = entered.Lifetime!;

        if (storageEngineRuntime.Disposed)
        {
            return;
        }

        var security = await engineResources.EnterSecurityEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var securityLifetime = security.SecurityLifetime!;
        storageEngineRuntime.Disposed = true;

        if (storageEngineRuntime.Quarantined)
        {
            await engineResources.DiscardBufferEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        await engineResources.CloseEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private static void ValidateStorageSessionContextEngineLifecycle(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Engine?.ResourceSource is not EngineResourceContext, nameof(StorageSessionContext.Engine)), (storageSessionContext?.Execution is null, nameof(StorageSessionContext.Execution)), (storageSessionContext?.Execution?.Operation == "Statement" && (storageSessionContext.Execution.OperationCallback is null || storageSessionContext.Execution.CallbackSource is null), nameof(StorageEngineExecutionContext.OperationCallback))]);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
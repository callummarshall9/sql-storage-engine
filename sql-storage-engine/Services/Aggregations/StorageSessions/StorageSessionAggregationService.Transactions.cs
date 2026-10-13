// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextTransactionAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTransaction(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        ValidatePublicTableInputs(inputs: [(storageSessionContext.TransactionRequest is null, nameof(StorageSessionContext.TransactionRequest))]);
        var transaction = storageSessionContext.Transaction!;
        var request = storageSessionContext.TransactionRequest!;

        switch (request.Operation)
        {
            case "Execute":
                ValidatePublicTableInputs(inputs: [(request.Callback is null || request.CallbackSource is null, nameof(StorageTransactionRequestContext.Callback))]);
                await ExecutePublicTransactionCallbackAsync(storagePublicTransactionContext: transaction, operation: request.Callback!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Commit":
                request.Receipt = await CommitPublicTransactionAsync(storagePublicTransactionContext: transaction, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Rollback":
                request.Receipt = await RollbackPublicTransactionAsync(storagePublicTransactionContext: transaction)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "CreateSavepoint":
                request.SavepointResult = await CreatePublicTransactionStorageSavepointAsync(storagePublicTransactionContext: transaction, name: request.Name!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "RollbackSavepoint":
                await RollbackPublicTransactionSavepointAsync(storagePublicTransactionContext: transaction, name: request.Name, storageSavepoint: request.Savepoint, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Dispose":
                await DisposePublicTransactionAsync(storagePublicTransactionContext: transaction)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentException(message: "Unknown transaction operation.", paramName: nameof(StorageTransactionRequestContext.Operation));
        }

        return storageSessionContext;
    });

    private void EnterPublicTransactionOperation(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        ObjectDisposedException.ThrowIf(condition: storagePublicTransactionContext.Disposed, instance: storagePublicTransactionContext);
        engineResources.EnterTransactionOperationEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Runtime!);
    }

    private void ReleasePublicTransactionOperation(StoragePublicTransactionContext storagePublicTransactionContext) =>
        engineResources.ReleaseTransactionOperationEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Runtime!);

    private EngineResourceContext LinkPublicTransactionToken(StoragePublicTransactionContext storagePublicTransactionContext, CancellationToken cancellationToken) =>
        engineResources.LinkTransactionTokenEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Runtime!with { InputToken = cancellationToken });

    private async ValueTask ExecutePublicTransactionCallbackAsync(StoragePublicTransactionContext storagePublicTransactionContext, Func<StorageSessionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken)
    {
        EnterPublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);

        try
        {
            if (storagePublicTransactionContext.State != StorageTransactionState.Committable)
            {
                throw new InvalidOperationException(message: "Transaction is not committable.");
            }

            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Lease!)
            .Lifetime!;

            var linked = LinkPublicTransactionToken(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: cancellationToken);
            using var linkedLifetime = linked.Lifetime!;
            Task? execution = null;

            try
            {
                linked.TokenResult.ThrowIfCancellationRequested();

                execution = ExecutePublicStatementCoreAsync(storageEngineRuntime: storagePublicTransactionContext.Engine!, operation: operation, cancellationToken: linked.TokenResult, transaction: storagePublicTransactionContext)
            .AsTask();

                await engineResources.WaitTransactionTaskEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Runtime!with { TaskSource = execution, Timeout = Timeout.InfiniteTimeSpan }, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (OperationCanceledException)
            {
                if (execution is { IsCompleted: false })
                {
                    try
                    {
                        await engineResources.WaitTransactionTaskEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Runtime!with { TaskSource = execution, Timeout = TimeSpan.FromMilliseconds(milliseconds: 250) }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                    catch (TimeoutException)when (!execution.IsCompleted)
                    {
                        storagePublicTransactionContext.State = StorageTransactionState.Indeterminate;
                        storagePublicTransactionContext.Engine!.Quarantined = true;
                        storagePublicTransactionContext.Engine.PendingCleanup = FinishPublicPendingCallbackAsync(storagePublicTransactionContext: storagePublicTransactionContext, execution: execution);
                        throw new OperationCanceledException(message: "Transaction callback has not stopped; storage remains quarantined.", token: linked.TokenResult);
                    }
                    catch
                    {
                    }
                }

                await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);

                throw;
            }
            catch (AggregateException)
            {
                storagePublicTransactionContext.State = StorageTransactionState.Doomed;
                throw new InvalidOperationException(message: "Transaction statement cleanup failed; only rollback is permitted.");
            }
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async Task FinishPublicPendingCallbackAsync(StoragePublicTransactionContext storagePublicTransactionContext, Task execution)
    {
        try
        {
            await execution.ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
        }

        await engineResources.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Runtime!, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async Task ExpirePublicTransactionAsync(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        await engineResources.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Runtime!, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (storagePublicTransactionContext.State is StorageTransactionState.Committable or StorageTransactionState.Doomed)
            {
                await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch
        {
            MarkPublicTransactionIndeterminate(storagePublicTransactionContext: storagePublicTransactionContext);
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async ValueTask<StorageTransactionReceipt> CommitPublicTransactionAsync(StoragePublicTransactionContext storagePublicTransactionContext, CancellationToken cancellationToken)
    {
        EnterPublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);

        try
        {
            if (storagePublicTransactionContext.State == StorageTransactionState.Committed)
            {
                return new StorageTransactionReceipt
                {
                    Identity = storagePublicTransactionContext.Identity,
                    State = storagePublicTransactionContext.State
                };
            }

            if (storagePublicTransactionContext.State != StorageTransactionState.Committable)
            {
                throw new InvalidOperationException(message: "Transaction cannot commit.");
            }

            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Lease!)
            .Lifetime!;

            var linked = LinkPublicTransactionToken(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: cancellationToken);
            using var linkedLifetime = linked.Lifetime!;
            var decisionStarted = false;

            try
            {
                await FlushPublicTransactionAsync(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

                linked.TokenResult.ThrowIfCancellationRequested();
                storagePublicTransactionContext.Engine!.TransactionObserver?.Invoke(obj: "BeforeCommitDecision");
                decisionStarted = true;

                await engineResources.CommitJournalEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Journal!with { Retain = true }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

                storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "AfterCommitDecision");

                await CompletePublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext, state: StorageTransactionState.Committed)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch
            {
                if (decisionStarted)
                {
                    MarkPublicTransactionIndeterminate(storagePublicTransactionContext: storagePublicTransactionContext);
                }
                else
                {
                    await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
            }

            return new StorageTransactionReceipt
            {
                Identity = storagePublicTransactionContext.Identity,
                State = storagePublicTransactionContext.State
            };
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async ValueTask<StorageTransactionReceipt> RollbackPublicTransactionAsync(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        EnterPublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);

        try
        {
            if (storagePublicTransactionContext.State == StorageTransactionState.Aborted)
            {
                return new StorageTransactionReceipt
                {
                    Identity = storagePublicTransactionContext.Identity,
                    State = storagePublicTransactionContext.State
                };
            }

            if (storagePublicTransactionContext.State is not (StorageTransactionState.Committable or StorageTransactionState.Doomed))
            {
                throw new InvalidOperationException(message: "Transaction cannot roll back.");
            }

            await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);

            return new StorageTransactionReceipt
            {
                Identity = storagePublicTransactionContext.Identity,
                State = storagePublicTransactionContext.State
            };
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async ValueTask AbortPublicTransactionCoreAsync(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        EngineResourceContext? cleanup = null;

        try
        {
            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Lease!)
            .Lifetime!;

            var resources = (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!;

            cleanup = engineResources.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: resources with { Timeout = TimeSpan.FromSeconds(seconds: 30), ExpiryNotification = () =>
            {
            } });

            engineResources.StartTransactionRuntimeEngineResourceContext(engineResourceContext: cleanup);
            storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "BeforeRollback");

            await RestorePublicEngineJournalAsync(storageEngineRuntime: storagePublicTransactionContext.Engine, journal: storagePublicTransactionContext.Journal!, retain: true, cancellationToken: cleanup.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            await CompletePublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext, state: StorageTransactionState.Aborted)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            MarkPublicTransactionIndeterminate(storagePublicTransactionContext: storagePublicTransactionContext);
        }
        finally
        {
            if (cleanup is not null)
            {
                engineResources.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: cleanup);
            }
        }
    }

    private async ValueTask CompletePublicTransactionCoreAsync(StoragePublicTransactionContext storagePublicTransactionContext, StorageTransactionState state)
    {
        var resources = (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!;

        await engineResources.WriteReceiptEngineResourceContextAsync(engineResourceContext: resources with { Receipt = new StorageTransactionReceipt { Identity = storagePublicTransactionContext.Identity, State = state } }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "AfterReceipt");
        engineResources.RemoveSavepointsEngineResourceContext(engineResourceContext: resources);
        engineResources.RemoveJournalEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Journal!);
        engineResources.RemoveFileEngineResourceContext(engineResourceContext: resources with { Path = ReadPublicTransactionActivePath(engineResourceContext: resources) });
        storagePublicTransactionContext.State = state;
        ReleasePublicTransaction(storagePublicTransactionContext: storagePublicTransactionContext, quarantine: false);
    }

    private void MarkPublicTransactionIndeterminate(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        storagePublicTransactionContext.State = StorageTransactionState.Indeterminate;
        ReleasePublicTransaction(storagePublicTransactionContext: storagePublicTransactionContext, quarantine: true);
    }

    private void ReleasePublicTransaction(StoragePublicTransactionContext storagePublicTransactionContext, bool quarantine)
    {
        if (storagePublicTransactionContext.Released)
        {
            return;
        }

        storagePublicTransactionContext.Released = true;
        var engine = storagePublicTransactionContext.Engine!;
        engine.Quarantined |= quarantine;

        if (ReferenceEquals(objA: engine.ActiveTransaction, objB: storagePublicTransactionContext))
        {
            engine.ActiveTransaction = null;
        }

        engineResources.SetMaximumLengthEngineResourceContext(engineResourceContext: (EngineResourceContext)engine.ResourceSource!with { MaximumLength = null });
        storagePublicTransactionContext.Lease!.Lifetime!.Dispose();
    }

    private async ValueTask FlushPublicTransactionAsync(StoragePublicTransactionContext storagePublicTransactionContext, CancellationToken cancellationToken)
    {
        await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTransactionContext.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await engineResources.FlushDatabaseEngineResourceContextAsync(engineResourceContext: (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask DisposePublicTransactionAsync(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        if (storagePublicTransactionContext.Disposed)
        {
            return;
        }

        var resources = (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!;

        if (engineResources.ReadEngineResourceContextAccess(engineResourceContext: resources)
            .Access is not null)
        {
            throw new InvalidOperationException(message: "Dispose the transaction after its callback returns.");
        }

        engineResources.CancelTransactionRuntimeEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Runtime!);

        await engineResources.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: storagePublicTransactionContext.Runtime!, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (storagePublicTransactionContext.Disposed)
            {
                return;
            }

            if (storagePublicTransactionContext.State is StorageTransactionState.Committable or StorageTransactionState.Doomed)
            {
                await AbortPublicTransactionCoreAsync(storagePublicTransactionContext: storagePublicTransactionContext)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            storagePublicTransactionContext.Disposed = true;
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }

        if (storagePublicTransactionContext.TimeoutCleanup is not null)
        {
            await storagePublicTransactionContext.TimeoutCleanup.ConfigureAwait(continueOnCapturedContext: false);
        }

        engineResources.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Runtime!);

        if (storagePublicTransactionContext.State == StorageTransactionState.Indeterminate)
        {
            throw new InvalidOperationException(message: "Transaction outcome is indeterminate; reopen storage to recover.");
        }
    }
}
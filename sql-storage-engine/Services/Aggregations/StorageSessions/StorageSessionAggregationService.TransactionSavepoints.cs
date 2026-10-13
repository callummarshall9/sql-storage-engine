// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private static void EnsurePublicSavepointState(StoragePublicTransactionContext storagePublicTransactionContext)
    {
        if (storagePublicTransactionContext.State != StorageTransactionState.Committable)
        {
            throw new InvalidOperationException(message: "Savepoints require a committable transaction.");
        }
    }

    private static void ValidatePublicSavepointName(string name)
    {
        ValidatePublicTableInputs(inputs: [(string.IsNullOrEmpty(value: name) || name.Length > StorageSavepoint.MaximumNameLength, nameof(name))]);
    }

    private async ValueTask<StorageSavepoint> CreatePublicTransactionStorageSavepointAsync(StoragePublicTransactionContext storagePublicTransactionContext, string name, CancellationToken cancellationToken)
    {
        EnterPublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);

        try
        {
            EnsurePublicSavepointState(storagePublicTransactionContext: storagePublicTransactionContext);
            ValidatePublicSavepointName(name: name);

            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Lease!)
            .Lifetime!;

            var linked = LinkPublicTransactionToken(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: cancellationToken);
            using var linkedLifetime = linked.Lifetime!;
            linked.TokenResult.ThrowIfCancellationRequested();

            if (storagePublicTransactionContext.Savepoints!.Count == StorageSavepoint.MaximumCount)
            {
                throw new InvalidOperationException(message: "Savepoint count quota exceeded.");
            }

            await FlushPublicTransactionAsync(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            var resources = (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!;

            var bytes = checked(engineResources.ReadFileEngineResourceContext(engineResourceContext: resources)
            .Length + 64);

            if (bytes > storagePublicTransactionContext.Options!.MaximumJournalBytes - storagePublicTransactionContext.SavepointBytes)
            {
                throw new InvalidOperationException(message: "Savepoint image quota exceeded.");
            }

            storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "BeforeSavepointCreate");

            var journal = await CreatePublicSavepointEngineResourceContextAsync(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "AfterSavepointCreate");
                linked.TokenResult.ThrowIfCancellationRequested();

                var point = new StorageSavepoint
                {
                    Transaction = storagePublicTransactionContext.Identity,
                    Value = engineResources.GenerateTransactionIdentityEngineResourceContext(engineResourceContext: resources)
            .DatabaseIdentity,
                    Name = name
                };

                storagePublicTransactionContext.Savepoints.Add(item: new StoragePublicSavepointEntry { Token = point, Journal = journal, Bytes = bytes });
                storagePublicTransactionContext.SavepointBytes += bytes;
                return point;
            }
            catch
            {
                DeletePublicSavepointImage(journal: journal);
                throw;
            }
        }
        catch (AggregateException)
        {
            storagePublicTransactionContext.State = StorageTransactionState.Doomed;
            throw;
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }

    private async ValueTask<EngineResourceContext> CreatePublicSavepointEngineResourceContextAsync(StoragePublicTransactionContext storagePublicTransactionContext, CancellationToken cancellationToken)
    {
        var resources = (EngineResourceContext)storagePublicTransactionContext.Engine!.ResourceSource!;

        var path = engineResources.GenerateSavepointPathEngineResourceContext(engineResourceContext: resources)
            .TextResult!;

        try
        {
            return await engineResources.CreateJournalEngineResourceContextAsync(engineResourceContext: resources with { ExplicitPath = path }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception error)
        {
            try
            {
                engineResources.RemoveFileEngineResourceContext(engineResourceContext: resources with { Path = path });
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(message: "Savepoint image cleanup failed.", innerExceptions: [error, cleanup]);
            }

            throw;
        }
    }

    private void DeletePublicSavepointImage(EngineResourceContext journal)
    {
        try
        {
            engineResources.RemoveJournalEngineResourceContext(engineResourceContext: journal);
        }
        catch (Exception error)
        {
            throw new AggregateException(message: "Savepoint image cleanup failed; root rollback is required.", innerExceptions: [error]);
        }
    }

    private async ValueTask RollbackPublicTransactionSavepointAsync(StoragePublicTransactionContext storagePublicTransactionContext, string? name, StorageSavepoint? storageSavepoint, CancellationToken cancellationToken)
    {
        EnterPublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);

        try
        {
            EnsurePublicSavepointState(storagePublicTransactionContext: storagePublicTransactionContext);

            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: storagePublicTransactionContext.Lease!)
            .Lifetime!;

            var linked = LinkPublicTransactionToken(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: cancellationToken);
            using var linkedLifetime = linked.Lifetime!;

            if (name is not null)
            {
                ValidatePublicSavepointName(name: name);
                storageSavepoint = storagePublicTransactionContext.Savepoints!.LastOrDefault(predicate: entry => StringComparer.Ordinal.Equals(x: entry.Token!.Name, y: name))?.Token ?? throw new ArgumentException(message: "Unknown savepoint name.", paramName: nameof(name));
            }

            var point = storageSavepoint ?? throw new ArgumentNullException(paramName: nameof(storageSavepoint));
            var index = storagePublicTransactionContext.Savepoints!.FindIndex(match: entry => entry.Token == point);

            if (point.Transaction != storagePublicTransactionContext.Identity || index < 0)
            {
                throw new ArgumentException(message: "Unknown, foreign or invalidated savepoint.", paramName: nameof(storageSavepoint));
            }

            linked.TokenResult.ThrowIfCancellationRequested();

            await FlushPublicTransactionAsync(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            var guard = await CreatePublicSavepointEngineResourceContextAsync(storagePublicTransactionContext: storagePublicTransactionContext, cancellationToken: linked.TokenResult)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                storagePublicTransactionContext.Engine!.TransactionObserver?.Invoke(obj: "BeforeSavepointRestore");
                linked.TokenResult.ThrowIfCancellationRequested();

                try
                {
                    await RestorePublicEngineJournalAsync(storageEngineRuntime: storagePublicTransactionContext.Engine, journal: storagePublicTransactionContext.Savepoints[index].Journal!, retain: true)
            .ConfigureAwait(continueOnCapturedContext: false);

                    storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "AfterSavepointRestore");
                }
                catch (Exception restoreError)
                {
                    try
                    {
                        storagePublicTransactionContext.Engine.TransactionObserver?.Invoke(obj: "BeforeSavepointRecovery");

                        await RestorePublicEngineJournalAsync(storageEngineRuntime: storagePublicTransactionContext.Engine, journal: guard, retain: true)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                    catch (Exception recoveryError)
                    {
                        throw new AggregateException(message: "Savepoint recovery failed; root rollback is required.", innerExceptions: [restoreError, recoveryError]);
                    }

                    throw new sql_storage_engine.Models.Exceptions.TransactionRecoveryEvidenceException(message: "Savepoint restoration failed; the pre-operation image was recovered.", innerException: restoreError);
                }

                var invalidated = storagePublicTransactionContext.Savepoints.Skip(count: index + 1)
            .ToArray();

                storagePublicTransactionContext.Savepoints.RemoveRange(index: index + 1, count: storagePublicTransactionContext.Savepoints.Count - index - 1);

                foreach (var entry in invalidated)
                {
                    storagePublicTransactionContext.SavepointBytes -= entry.Bytes;
                    DeletePublicSavepointImage(journal: entry.Journal!);
                }
            }
            finally
            {
                DeletePublicSavepointImage(journal: guard);
            }
        }
        catch (AggregateException)
        {
            storagePublicTransactionContext.State = StorageTransactionState.Doomed;
            throw;
        }
        finally
        {
            ReleasePublicTransactionOperation(storagePublicTransactionContext: storagePublicTransactionContext);
        }
    }
}
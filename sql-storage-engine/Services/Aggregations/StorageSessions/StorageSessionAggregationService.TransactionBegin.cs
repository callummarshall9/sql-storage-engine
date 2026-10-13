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
    public ValueTask<StorageSessionContext> BeginStorageSessionContextTransactionAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTransactionBegin(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var engine = storageSessionContext.Engine!;
        var options = storageSessionContext.TransactionRequest!.Options!;
        var resources = (EngineResourceContext)engine.ResourceSource!;

        if (engineResources.ReadEngineResourceContextAccess(engineResourceContext: resources)
            .Access is not null)
        {
            throw new InvalidOperationException(message: "Nested transaction acquisition is not supported.");
        }

        EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = engine, Generation = engine.Generation } });

        var lease = await engineResources.EnterEngineResourceContextAsync(engineResourceContext: resources with { Reentrant = false }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        EngineResourceContext? journal = null;

        var identity = new StorageTransactionIdentity
        {
            DatabaseId = resources.Header!.DatabaseId,
            Value = engineResources.GenerateTransactionIdentityEngineResourceContext(engineResourceContext: resources)
            .DatabaseIdentity
        };

        try
        {
            using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

            EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = engine, Generation = engine.Generation } });

            await FlushPublicEngineAsync(storageEngineRuntime: engine, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await engineResources.FlushDatabaseEngineResourceContextAsync(engineResourceContext: resources, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (engineResources.ReadFileEngineResourceContext(engineResourceContext: resources)
            .Length > options.MaximumJournalBytes)
            {
                throw new InvalidOperationException(message: "Database exceeds the transaction journal quota.");
            }

            journal = await engineResources.CreateJournalEngineResourceContextAsync(engineResourceContext: resources with { ExplicitPath = ReadPublicTransactionJournalPath(engineResourceContext: resources) }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await engineResources.WriteMetadataEngineResourceContextAsync(engineResourceContext: resources with { Path = ReadPublicTransactionActivePath(engineResourceContext: resources), Metadata = identity }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            cancellationToken.ThrowIfCancellationRequested();
            engineResources.SetMaximumLengthEngineResourceContext(engineResourceContext: resources with { MaximumLength = options.MaximumJournalBytes });

            var transaction = new StoragePublicTransactionContext
            {
                Engine = engine,
                Lease = lease,
                Journal = journal,
                Identity = identity,
                State = StorageTransactionState.Committable,
                Options = options,
                Savepoints = []
            };

            transaction.Runtime = engineResources.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: resources with { Timeout = options.Timeout, ExpiryNotification = () => transaction.TimeoutCleanup = ExpirePublicTransactionAsync(storagePublicTransactionContext: transaction) });
            engine.ActiveTransaction = transaction;
            engineResources.StartTransactionRuntimeEngineResourceContext(engineResourceContext: transaction.Runtime);

            storageSessionContext.TransactionRequest.Result = new StorageSessionContext
            {
                Binding = new StorageSessionBinding
                {
                    Service = this,
                    Engine = engine
                },
                Engine = engine,
                Transaction = transaction
            };

            return storageSessionContext;
        }
        catch
        {
            if (journal is not null)
            {
                try
                {
                    await engineResources.RestoreJournalEngineResourceContextAsync(engineResourceContext: journal with { Retain = true }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

                    await engineResources.WriteReceiptEngineResourceContextAsync(engineResourceContext: resources with { Receipt = new StorageTransactionReceipt { Identity = identity, State = StorageTransactionState.Aborted } }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

                    engineResources.RemoveJournalEngineResourceContext(engineResourceContext: journal);
                    engineResources.RemoveFileEngineResourceContext(engineResourceContext: resources with { Path = ReadPublicTransactionActivePath(engineResourceContext: resources) });
                }
                catch
                {
                    engine.Quarantined = true;
                    lease.Lifetime!.Dispose();
                    throw new StorageTransactionBeginException(identity: identity);
                }
            }

            lease.Lifetime!.Dispose();
            throw;
        }
    });

    public StorageSessionContext ReadStorageSessionContextTransaction(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageSessionContextTransaction(storageSessionContext: storageSessionContext, cancellationToken: CancellationToken.None);
        return storageSessionContext;
    });

    private string ReadPublicTransactionJournalPath(EngineResourceContext engineResourceContext) =>
        engineResources.ReadTransactionJournalPathEngineResourceContext(engineResourceContext: engineResourceContext)
            .TextResult!;

    private string ReadPublicTransactionActivePath(EngineResourceContext engineResourceContext) =>
        engineResources.ReadTransactionActivePathEngineResourceContext(engineResourceContext: engineResourceContext)
            .TextResult!;

    private static void ValidateStorageSessionContextTransactionBegin(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Engine?.ResourceSource is not EngineResourceContext, nameof(StorageSessionContext.Engine)), (storageSessionContext?.TransactionRequest?.Options is null, nameof(StorageTransactionRequestContext.Options))]);
        var options = storageSessionContext!.TransactionRequest!.Options!;
        ValidateTransactionOptionInputs(inputs: [(options.Timeout <= TimeSpan.Zero || options.Timeout.Ticks > TimeSpan.TicksPerDay, nameof(StorageTransactionOptions.Timeout), options.Timeout), (options.Isolation != StorageTransactionIsolation.Serializable, nameof(StorageTransactionOptions.Isolation), options.Isolation), (options.MaximumJournalBytes <= 0 || options.MaximumJournalBytes > StorageTransactionOptions.MaximumSupportedJournalBytes, nameof(StorageTransactionOptions.MaximumJournalBytes), options.MaximumJournalBytes)]);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(message: "Explicit transaction directory durability currently requires Linux.");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionOptionInputs((bool Invalid, string ParameterName, object Value)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new sql_storage_engine.Models.Exceptions.StorageModelValidationException(innerException: new ArgumentOutOfRangeException(paramName: input.ParameterName, actualValue: input.Value, message: "Unsupported storage transaction option."));
            }
        }
    }

    private static void ValidateStorageSessionContextTransaction(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Transaction?.Engine?.ResourceSource is not EngineResourceContext, nameof(StorageSessionContext.Transaction)), (storageSessionContext?.Transaction?.Runtime is null, nameof(StoragePublicTransactionContext.Runtime))]);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
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
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextReceiptAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextReceipt(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var engine = storageSessionContext.Engine!;
        var request = storageSessionContext.TransactionRequest!;
        var resources = (EngineResourceContext)engine.ResourceSource!;
        ObjectDisposedException.ThrowIf(condition: engine.Disposed, instance: engine);
        var identity = request.Identity;

        if (identity.Value == Guid.Empty || identity.DatabaseId != resources.Header!.DatabaseId)
        {
            throw new ArgumentException(message: "Transaction identity belongs to another database.", paramName: nameof(StorageTransactionRequestContext.Identity));
        }

        if (request.Operation == "Resolve")
        {
            request.Receipt = (await engineResources.ResolveReceiptEngineResourceContextAsync(engineResourceContext: resources with { Receipt = new StorageTransactionReceipt { Identity = identity } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Receipt;

            return storageSessionContext;
        }

        if (request.Operation != "Release")
        {
            throw new ArgumentException(message: "Unknown transaction receipt operation.", paramName: nameof(StorageTransactionRequestContext.Operation));
        }

        var lease = await EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = new StorageHandleContext { Engine = engine, Generation = engine.Generation } }, cancellationToken: cancellationToken, reentrant: true)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = lease.Lifetime!;

        using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        if ((await engineResources.ResolveReceiptEngineResourceContextAsync(engineResourceContext: resources with { Receipt = new StorageTransactionReceipt { Identity = identity } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Receipt!.State == StorageTransactionState.Indeterminate)
        {
            throw new InvalidOperationException(message: "An unresolved transaction cannot release its receipt.");
        }

        engineResources.ReleaseReceiptEngineResourceContext(engineResourceContext: resources with { Receipt = new StorageTransactionReceipt { Identity = identity } });
        return storageSessionContext;
    });

    private static void ValidateStorageSessionContextReceipt(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Engine?.ResourceSource is not EngineResourceContext, nameof(StorageSessionContext.Engine)), (storageSessionContext?.TransactionRequest is null, nameof(StorageSessionContext.TransactionRequest))]);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
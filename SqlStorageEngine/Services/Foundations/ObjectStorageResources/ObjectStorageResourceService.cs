// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using sql_storage_engine.Brokers.ObjectStorageResources;

namespace sql_storage_engine.Services.Foundations.ObjectStorageResources;
internal sealed partial class ObjectStorageResourceService : IObjectStorageResourceService
{
    private readonly ObjectStorageResourceBroker resource;

    public ObjectStorageResourceService(ObjectStorageResourceBroker resource)
    {
        this.resource = resource;
    }

    internal static ObjectStorageResourceService CreateObjectStorageResourceService(StorageObjectResourceConfiguration? objectResourceConfiguration) =>
        new ObjectStorageResourceService(resource: new ObjectStorageResourceBroker(configuration: objectResourceConfiguration));

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageStrategyContext(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        switch (storageStrategyContext.Operation)
        {
            case 201:
                ArgumentException.ThrowIfNullOrWhiteSpace(argument: storageStrategyContext.Key);

                var metadata = await resource.ReadAsync(key: storageStrategyContext.Key, maximumBytes: storageStrategyContext.MaximumBytes, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                storageStrategyContext.Bytes = metadata?.Bytes;

                storageStrategyContext.MetadataVersion = metadata?.Version;
                break;
            case 202:
                await resource.UploadAsync(objects: storageStrategyContext.Uploads!, concurrency: storageStrategyContext.State!.Options!.UploadConcurrency, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 203:
                storageStrategyContext.MetadataVersion = await resource.PublishAsync(key: storageStrategyContext.Key!, bytes: storageStrategyContext.Bytes!, expectedVersion: storageStrategyContext.MetadataVersion, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 204:
                storageStrategyContext.Bytes = resource.CalculateChecksum(bytes: storageStrategyContext.Source.Span);
                break;
            case 205:
                storageStrategyContext.Runtime = resource.CreateIdentity();
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }

        return storageStrategyContext;
    });
}
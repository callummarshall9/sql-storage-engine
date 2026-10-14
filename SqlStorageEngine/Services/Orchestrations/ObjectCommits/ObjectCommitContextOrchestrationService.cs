// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.ObjectStorageResources;
using sql_storage_engine.Services.Foundations.ObjectManifests;

namespace sql_storage_engine.Services.Orchestrations.ObjectCommits;
internal sealed partial class ObjectCommitContextOrchestrationService : IObjectCommitContextOrchestrationService
{
    private readonly ObjectStorageResourceService objects;

    private readonly StorageObjectManifestService manifests;

    public ObjectCommitContextOrchestrationService(ObjectStorageResourceService objects, StorageObjectManifestService manifests)
    {
        this.objects = objects;

        this.manifests = manifests;
    }

    internal static ObjectCommitContextOrchestrationService CreateObjectCommitContextOrchestrationService(StorageObjectResourceConfiguration? objectResourceConfiguration) =>
        new ObjectCommitContextOrchestrationService(objects: ObjectStorageResourceService.CreateObjectStorageResourceService(objectResourceConfiguration: objectResourceConfiguration), manifests: new StorageObjectManifestService());

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageStrategyContext(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        if (storageStrategyContext.Operation == 210)
        {
            return manifests.EncodeStorageStrategyContext(storageStrategyContext: storageStrategyContext);
        }

        if (storageStrategyContext.Operation == 211)
        {
            return manifests.DecodeStorageStrategyContext(storageStrategyContext: storageStrategyContext);
        }

        return await objects.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
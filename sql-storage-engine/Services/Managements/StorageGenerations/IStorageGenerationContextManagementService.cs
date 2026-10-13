// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.StorageGenerations;
internal interface IStorageGenerationContextManagementService
{
    ValueTask<StorageGenerationContext> AllocateStorageGenerationContextRowVersionAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken);

    ValueTask<StorageGenerationContext> AllocateStorageGenerationContextGeneratedValueAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken);

    StorageGenerationContext ReadStorageGenerationContextTimestamp(StorageGenerationContext storageGenerationContext);

    ValueTask PublishStorageGenerationContextCatalogRootAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using Azure.Storage.Blobs;
using sql_storage_engine.AzureBlob.Models;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.AzureBlob.Exposures;
public static class StorageStrategyBuilderExposureExtensions
{
    public static StorageStrategyBuilderExposure UseBlobStorage(this StorageStrategyBuilderExposure builder, BlobContainerClient container, StorageStrategyOptions? storageStrategyOptions = null) =>
        builder.UseObjectStorage(objectStoreType: typeof(AzureBlobObjectStoreExposure), objectStoreArguments: [new AzureBlobStorageConfiguration { ContainerSource = container, TransferSize = storageStrategyOptions?.SegmentSize ?? 0, UploadConcurrency = storageStrategyOptions?.UploadConcurrency ?? 0 }], storageStrategyOptions: storageStrategyOptions);
}
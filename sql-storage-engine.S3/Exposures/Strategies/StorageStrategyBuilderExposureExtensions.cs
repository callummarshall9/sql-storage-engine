// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using Amazon.S3;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.S3.Exposures.Strategies;

namespace sql_storage_engine.Exposures.Strategies;
public static class StorageStrategyBuilderExposureExtensions
{
    public static StorageStrategyBuilderExposure UseS3(this StorageStrategyBuilderExposure builder, IAmazonS3 client, string bucket, StorageStrategyOptions? storageStrategyOptions = null) =>
        builder.UseObjectStorage(objectStoreType: typeof(S3StorageObjectStoreExposure), objectStoreArguments: new object[] { client, bucket }, storageStrategyOptions: storageStrategyOptions);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Services.Foundations.ObjectManifests;
internal interface IStorageObjectManifestService
{
    StorageStrategyContext EncodeStorageStrategyContext(StorageStrategyContext storageStrategyContext);
    StorageStrategyContext DecodeStorageStrategyContext(StorageStrategyContext storageStrategyContext);
}
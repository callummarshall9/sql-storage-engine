// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Services.Foundations.StorageRegistrations;

namespace sql_storage_engine.Exposures.Strategies;
public sealed class StorageStrategyBuilderExposure : ICompositionExposure
{
    private readonly StorageRegistrationService service;
    internal StorageStrategyBuilderExposure(StorageRegistrationConfiguration storageRegistrationConfiguration)
    {
        service = StorageRegistrationService.CreateStorageRegistrationService(storageRegistrationConfiguration: storageRegistrationConfiguration);
    }

    public StorageStrategyBuilderExposure UseLocalFileSystem(string directory) =>
        UseLocalFileSystem(storageStrategyOptions: new StorageStrategyOptions { Directory = directory });

    public StorageStrategyBuilderExposure UseLocalFileSystem(StorageStrategyOptions storageStrategyOptions)
    {
        service.RegisterStorageStrategyRegistration(storageStrategyRegistration: new StorageStrategyRegistration { StrategyType = typeof(FileStorageStrategyExposure), Options = storageStrategyOptions });
        return this;
    }

    public StorageStrategyBuilderExposure UseObjectStorage(IStorageObjectStoreExposure objectStore, StorageStrategyOptions? storageStrategyOptions = null)
    {
        service.RegisterStorageStrategyRegistration(storageStrategyRegistration: new StorageStrategyRegistration { StrategyType = typeof(ObjectStorageStrategyExposure), ObjectStoreSource = objectStore, Options = storageStrategyOptions });
        return this;
    }

    public StorageStrategyBuilderExposure UseObjectStorage(Type objectStoreType, object[] objectStoreArguments, StorageStrategyOptions? storageStrategyOptions = null)
    {
        service.RegisterStorageStrategyRegistration(storageStrategyRegistration: new StorageStrategyRegistration { StrategyType = typeof(ObjectStorageStrategyExposure), ObjectStoreType = objectStoreType, ObjectStoreArguments = objectStoreArguments, Options = storageStrategyOptions });
        return this;
    }
}
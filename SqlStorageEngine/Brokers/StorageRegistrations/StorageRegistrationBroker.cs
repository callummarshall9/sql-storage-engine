// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.StorageRegistrations;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Brokers.StorageRegistrations;
internal sealed class StorageRegistrationBroker : IStorageRegistrationBroker
{
    private readonly StorageRegistrationDependency dependency;
    private readonly StorageRegistrationConfiguration configuration;
    public StorageRegistrationBroker(StorageRegistrationConfiguration storageRegistrationConfiguration)
    {
        configuration = storageRegistrationConfiguration;
        dependency = new StorageRegistrationDependency(services: storageRegistrationConfiguration.Services!);
    }

    public object? ReadDefaults(Type type) =>
        dependency.BindDefaults(configuration: configuration.Configuration, type: type, fallback: new StorageEngineConfiguration { DefaultOptions = configuration.Defaults }, adjustment: configuration.Adjustment);

    public void Register(Type serviceType, Type implementationType, object[] arguments, Type objectStoreType, object? objectStore, Type? objectImplementationType, object[]? objectArguments) =>
        dependency.Register(serviceType: serviceType, implementationType: implementationType, arguments: arguments, objectStoreType: objectStoreType, objectStore: objectStore, objectImplementationType: objectImplementationType, objectArguments: objectArguments);
}
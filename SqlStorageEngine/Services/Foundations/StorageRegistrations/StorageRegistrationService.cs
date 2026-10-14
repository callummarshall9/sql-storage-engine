// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Brokers.StorageRegistrations;

namespace sql_storage_engine.Services.Foundations.StorageRegistrations;
internal sealed partial class StorageRegistrationService(StorageRegistrationBroker broker) : IStorageRegistrationService
{
    internal static StorageRegistrationService CreateStorageRegistrationService(StorageRegistrationConfiguration storageRegistrationConfiguration) =>
        new StorageRegistrationService(broker: new StorageRegistrationBroker(storageRegistrationConfiguration: storageRegistrationConfiguration));

    public StorageStrategyRegistration RegisterStorageStrategyRegistration(StorageStrategyRegistration storageStrategyRegistration) =>
        TryCatch(operation: () =>
    {
        ValidateStorageStrategyRegistration(storageStrategyRegistration: storageStrategyRegistration);
        var defaults = (StorageEngineConfiguration?)broker.ReadDefaults(type: typeof(StorageEngineConfiguration));
        var options = storageStrategyRegistration.Options ?? defaults?.DefaultOptions ?? new StorageStrategyOptions();

        broker.Register(serviceType: typeof(IStorageStrategyExposure), implementationType: storageStrategyRegistration.StrategyType!, arguments: new object[] { options }, objectStoreType: typeof(IStorageObjectStoreExposure), objectStore: storageStrategyRegistration.ObjectStoreSource, objectImplementationType: storageStrategyRegistration.ObjectStoreType, objectArguments: storageStrategyRegistration.ObjectStoreArguments);
        return storageStrategyRegistration;
    });
}
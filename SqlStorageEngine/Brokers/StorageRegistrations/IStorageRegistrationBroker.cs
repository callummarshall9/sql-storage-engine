// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.StorageRegistrations;
internal interface IStorageRegistrationBroker
{
    object? ReadDefaults(Type type);

    void Register(Type serviceType, Type implementationType, object[] arguments, Type objectStoreType, object? objectStore, Type? objectImplementationType, object[]? objectArguments);
}
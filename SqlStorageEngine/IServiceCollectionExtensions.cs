// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine;
public static class IServiceCollectionExtensions
{
    public static StorageStrategyBuilderExposure AddSqlStorageEngine(this IServiceCollection services, StorageEngineConfiguration? storageEngineConfiguration = null) =>
        new StorageStrategyBuilderExposure(storageRegistrationConfiguration: new StorageRegistrationConfiguration { Services = services, Defaults = storageEngineConfiguration?.DefaultOptions });

    public static StorageStrategyBuilderExposure AddSqlStorageEngine(this IServiceCollection services, IConfiguration configuration, Action<StorageEngineConfiguration>? adjustment = null) =>
        new StorageStrategyBuilderExposure(storageRegistrationConfiguration: new StorageRegistrationConfiguration { Services = services, Configuration = configuration, Adjustment = adjustment });
}
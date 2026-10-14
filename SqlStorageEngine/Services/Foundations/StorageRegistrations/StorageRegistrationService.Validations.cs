// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Services.Foundations.StorageRegistrations;
internal sealed partial class StorageRegistrationService
{
    private static void ValidateStorageStrategyRegistration(StorageStrategyRegistration storageStrategyRegistration)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyRegistration);

        if (storageStrategyRegistration.StrategyType is null || (storageStrategyRegistration.ObjectStoreType is not null && storageStrategyRegistration.ObjectStoreArguments is null))
        {
            throw new ArgumentException(message: "A storage strategy is required.", paramName: nameof(storageStrategyRegistration));
        }
    }
}
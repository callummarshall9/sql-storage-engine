// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Services.Foundations.StorageRegistrations;
internal interface IStorageRegistrationService
{
    StorageStrategyRegistration RegisterStorageStrategyRegistration(StorageStrategyRegistration storageStrategyRegistration);
}
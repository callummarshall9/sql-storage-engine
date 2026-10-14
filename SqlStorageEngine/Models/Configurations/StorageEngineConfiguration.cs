// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Models.Configurations;
public sealed class StorageEngineConfiguration
{
    public StorageStrategyOptions? DefaultOptions { get; set; }
}
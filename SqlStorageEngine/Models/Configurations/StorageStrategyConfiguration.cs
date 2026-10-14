// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Models.Configurations;
internal sealed class StorageStrategyConfiguration
{
    public StorageStrategyOptions? Options { get; set; }
    public bool Remote { get; set; }
}
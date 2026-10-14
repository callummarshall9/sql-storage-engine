// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Strategies;
internal sealed record StorageStrategyRegistration
{
    public Type? StrategyType { get; init; }
    public Type? ObjectStoreType { get; init; }
    public object[]? ObjectStoreArguments { get; init; }
    public object? ObjectStoreSource { get; init; }
    public StorageStrategyOptions? Options { get; init; }
}
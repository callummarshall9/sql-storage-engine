// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
public sealed record LockManagerBinding
{
    internal object? Service { get; init; }
}
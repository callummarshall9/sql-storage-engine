// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
public sealed record LockingTransactionBinding
{
    internal object? Service { get; init; }
    internal LockingTransactionContext? State { get; init; }
}
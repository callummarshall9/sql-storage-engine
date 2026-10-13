// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
public readonly record struct TransactionId
{
    public ulong Value { get; init; }
}
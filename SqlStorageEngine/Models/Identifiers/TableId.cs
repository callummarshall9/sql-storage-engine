// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
public readonly record struct TableId
{
    public ulong Value { get; init; }
}
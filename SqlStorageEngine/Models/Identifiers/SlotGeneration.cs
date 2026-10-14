// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
public readonly record struct SlotGeneration
{
    public uint Value { get; init; }
}
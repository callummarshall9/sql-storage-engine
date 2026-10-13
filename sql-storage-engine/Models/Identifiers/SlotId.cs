// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
public readonly record struct SlotId
{
    public ushort Value { get; init; }
}
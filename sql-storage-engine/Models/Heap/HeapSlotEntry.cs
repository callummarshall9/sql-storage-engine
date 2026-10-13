// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Models.Heap;
/// <summary>Persistent address, size, state, and stale-reference generation for one slot.</summary>
public readonly record struct HeapSlotEntry
{
    public HeapSlotState State { get; init; }
    public uint Offset { get; init; }
    public uint Length { get; init; }
    public SlotGeneration Generation { get; init; }
}
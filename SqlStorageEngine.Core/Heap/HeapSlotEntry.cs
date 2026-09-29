using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

/// <summary>Persistent address, size, state, and stale-reference generation for one slot.</summary>
public readonly record struct HeapSlotEntry(
    HeapSlotState State,
    uint Offset,
    uint Length,
    SlotGeneration Generation);

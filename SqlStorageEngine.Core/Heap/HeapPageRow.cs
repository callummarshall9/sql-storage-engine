using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Heap;

public readonly record struct HeapPageRow(
    SlotId SlotId,
    SlotGeneration Generation,
    ReadOnlyMemory<byte> Bytes);

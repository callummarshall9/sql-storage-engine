using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Heap;

public enum HeapReadResult
{
    Found,
    UnknownSlot,
    Deleted,
    StaleGeneration
}

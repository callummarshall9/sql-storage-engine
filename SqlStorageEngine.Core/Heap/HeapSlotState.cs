using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

/// <summary>Persistent state of one heap slot.</summary>
public enum HeapSlotState : ushort
{
    Unused = 0,
    Live = 1,
    Deleted = 2
}

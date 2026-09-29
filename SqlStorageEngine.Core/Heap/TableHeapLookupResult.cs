using System.Diagnostics;
using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

public enum TableHeapLookupResult
{
    Found,
    UnknownPage,
    UnknownSlot,
    Deleted,
    StaleGeneration
}

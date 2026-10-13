// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Heap;
public enum TableHeapLookupResult
{
    Found,
    UnknownPage,
    UnknownSlot,
    Deleted,
    StaleGeneration
}
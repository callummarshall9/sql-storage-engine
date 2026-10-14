// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Heap;
/// <summary>Outcome of replacing a row on its current heap page.</summary>
public enum HeapUpdateResult
{
    Updated,
    Absent,
    RelocationRequired
}

public enum HeapReadResult
{
    Found,
    UnknownSlot,
    Deleted,
    StaleGeneration
}
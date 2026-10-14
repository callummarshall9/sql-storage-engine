// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Heap;

namespace sql_storage_engine.Models.Heap;
internal readonly record struct FreeSpaceEntry
{
    public int FreeBytes { get; init; }
    public FreeSpaceCategory Category { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Heap;
public enum FreeSpaceCategory
{
    None,
    Tiny,
    Small,
    Medium,
    Large
}
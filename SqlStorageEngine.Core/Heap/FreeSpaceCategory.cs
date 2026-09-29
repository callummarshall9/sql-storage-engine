using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Heap;

public enum FreeSpaceCategory
{
    None,
    Tiny,
    Small,
    Medium,
    Large
}

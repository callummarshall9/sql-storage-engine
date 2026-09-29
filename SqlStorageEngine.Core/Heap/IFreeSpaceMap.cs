using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Heap;

public interface IFreeSpaceMap
{
    PageId? FindPage(int requiredBytes);
    void Update(PageId pageId, int freeBytes);
    void Remove(PageId pageId);
    void Clear();
}

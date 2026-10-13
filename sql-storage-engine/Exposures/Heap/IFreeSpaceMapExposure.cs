// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Exposures.Heap;
public interface IFreeSpaceMapExposure
{
    PageId? FindPage(int requiredBytes);

    void Update(PageId updatedPageId, int freeBytes);

    void Remove(PageId pageId);

    void Clear();
}
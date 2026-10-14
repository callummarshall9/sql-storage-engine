// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Services.Processings.FreeSpaces;
internal interface IFreeSpacePageIdProcessingService
{
    int ReadFreeSpacePageSize();

    PageId? FindFreeSpacePageId(int requiredBytes);

    void UpdateFreeSpacePageId(ulong pageId, int freeBytes);

    void RemoveFreeSpacePageId(ulong pageId);

    void ClearFreeSpacePageIds();

    (bool Found, FreeSpaceCategory Category) ReadFreeSpaceCategory(ulong pageId);

    FreeSpaceCategory CategorizeFreeSpace(int freeBytes);
}
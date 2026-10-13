// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Processings.FreeSpaces;

namespace sql_storage_engine.Exposures.Heap;
public sealed class FreeSpaceMapExposure : sql_storage_engine.Exposures.Heap.IFreeSpaceMapExposure
{
    private readonly FreeSpacePageIdProcessingService service;
    public FreeSpaceMapExposure(int pageSize)
    {
        service = new FreeSpacePageIdProcessingService(configuration: new FreeSpaceConfiguration { PageSize = pageSize });
    }

    public int ReadPageSize() =>
        service.ReadFreeSpacePageSize();

    public PageId? FindPage(int requiredBytes) =>
        service.FindFreeSpacePageId(requiredBytes: requiredBytes);

    public void Update(PageId updatedPageId, int freeBytes) =>
        service.UpdateFreeSpacePageId(pageId: updatedPageId.Value, freeBytes: freeBytes);

    public void Remove(PageId pageId) =>
        service.RemoveFreeSpacePageId(pageId: pageId.Value);

    public void Clear() =>
        service.ClearFreeSpacePageIds();

    public bool TryGetCategory(PageId pageId, out FreeSpaceCategory category)
    {
        var result = service.ReadFreeSpaceCategory(pageId: pageId.Value);
        category = result.Category;
        return result.Found;
    }

    public FreeSpaceCategory Categorize(int freeBytes) =>
        service.CategorizeFreeSpace(freeBytes: freeBytes);
}
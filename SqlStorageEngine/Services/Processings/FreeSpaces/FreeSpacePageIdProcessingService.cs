// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Processings.FreeSpaces;
internal sealed partial class FreeSpacePageIdProcessingService : IFreeSpacePageIdProcessingService
{
    private readonly FreeSpaceState state;
    public FreeSpacePageIdProcessingService(FreeSpaceConfiguration configuration)
    {
        state = PrepareFreeSpaceState(configuration: configuration);
    }

    private static FreeSpaceState PrepareFreeSpaceState(FreeSpaceConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpaceConfiguration(configuration: configuration);

        return new FreeSpaceState
        {
            Sync = new object (),
            Entries = new Dictionary<ulong, FreeSpaceEntry>(),
            PageSize = configuration.PageSize
        };
    });

    public int ReadFreeSpacePageSize() =>
        TryCatch(operation: () =>
    {
        return state.PageSize;
    });

    public PageId? FindFreeSpacePageId(int requiredBytes) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpaceRequirement(requiredBytes: requiredBytes);

        lock (state.Sync)
        {
            ulong? selectedPageId = null;

            foreach (var pair in state.Entries)
            {
                if (pair.Value.FreeBytes >= requiredBytes && (selectedPageId is null || pair.Key < selectedPageId.Value))
                {
                    selectedPageId = pair.Key;
                }
            }

            return selectedPageId is { } pageId ? (PageId? )new PageId
            {
                Value = pageId
            }

            : null;
        }
    });

    public void UpdateFreeSpacePageId(ulong pageId, int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpacePageIdOnUpdate(pageId: pageId, freeBytes: freeBytes);

        lock (state.Sync)
        {
            state.Entries[pageId] = new FreeSpaceEntry
            {
                FreeBytes = freeBytes,
                Category = CategorizeCore(freeBytes: freeBytes)
            };
        }
    });

    public void RemoveFreeSpacePageId(ulong pageId) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpaceMapState(pageId: pageId);

        lock (state.Sync)
        {
            state.Entries.Remove(key: pageId);
        }
    });

    public void ClearFreeSpacePageIds() =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            state.Entries.Clear();
        }
    });

    public (bool Found, FreeSpaceCategory Category) ReadFreeSpaceCategory(ulong pageId) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpaceMapState(pageId: pageId);

        lock (state.Sync)
        {
            return state.Entries.TryGetValue(key: pageId, value: out var entry) ? (Found: true, Category: entry.Category) : (Found: false, Category: default(FreeSpaceCategory));
        }
    });

    public FreeSpaceCategory CategorizeFreeSpace(int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpaceCategoryInput(freeBytes: freeBytes);
        return CategorizeCore(freeBytes: freeBytes);
    });

    private FreeSpaceCategory CategorizeCore(int freeBytes)
    {
        if (freeBytes <= 0)
        {
            return FreeSpaceCategory.None;
        }

        var percent = checked(freeBytes * 100L / state.PageSize);

        return percent switch
        {
            <= 25 => FreeSpaceCategory.Tiny,
            <= 50 => FreeSpaceCategory.Small,
            <= 75 => FreeSpaceCategory.Medium,
            _ => FreeSpaceCategory.Large
        };
    }
}
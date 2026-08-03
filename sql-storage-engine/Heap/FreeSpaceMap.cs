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

public interface IFreeSpaceMap
{
    PageId? FindPage(int requiredBytes);
    void Update(PageId pageId, int freeBytes);
    void Remove(PageId pageId);
    void Clear();
}

/// <summary>A synchronized volatile map of coarse free-space categories and exact verified hints.</summary>
public sealed class InMemoryFreeSpaceMap : IFreeSpaceMap
{
    private readonly object _sync = new();
    private readonly Dictionary<PageId, Entry> _entries = [];

    public InMemoryFreeSpaceMap(int pageSize)
    {
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        PageSize = pageSize;
    }

    public int PageSize { get; }

    public PageId? FindPage(int requiredBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredBytes);
        lock (_sync)
        {
            foreach (var pair in _entries.OrderBy(pair => pair.Key.Value))
                if (pair.Value.FreeBytes >= requiredBytes) return pair.Key;
            return null;
        }
    }

    public void Update(PageId pageId, int freeBytes)
    {
        if (freeBytes < 0 || freeBytes > PageSize) throw new ArgumentOutOfRangeException(nameof(freeBytes));
        lock (_sync) _entries[pageId] = new Entry(freeBytes, Categorize(freeBytes));
    }

    public void Remove(PageId pageId) { lock (_sync) _entries.Remove(pageId); }
    public void Clear() { lock (_sync) _entries.Clear(); }

    public bool TryGetCategory(PageId pageId, out FreeSpaceCategory category)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(pageId, out var entry)) { category = entry.Category; return true; }
            category = default;
            return false;
        }
    }

    public FreeSpaceCategory Categorize(int freeBytes)
    {
        if (freeBytes <= 0) return FreeSpaceCategory.None;
        var percent = checked(freeBytes * 100L / PageSize);
        return percent switch
        {
            <= 25 => FreeSpaceCategory.Tiny,
            <= 50 => FreeSpaceCategory.Small,
            <= 75 => FreeSpaceCategory.Medium,
            _ => FreeSpaceCategory.Large
        };
    }

    private readonly record struct Entry(int FreeBytes, FreeSpaceCategory Category);
}

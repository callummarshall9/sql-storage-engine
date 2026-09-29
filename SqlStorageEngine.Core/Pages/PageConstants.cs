using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Page sizing and addressing rules.</summary>
public static class PageConstants
{
    public const int DefaultSize = 8192;
    public const int MinimumSize = 4096;
    public const int MaximumSize = 65536;

    public static bool IsSupportedSize(int pageSize) =>
        pageSize is >= MinimumSize and <= MaximumSize && (pageSize & (pageSize - 1)) == 0;

    public static long GetPageOffset(PageId pageId, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        return checked((long)checked(pageId.Value * (ulong)pageSize));
    }
}

using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Identifies the logical contents of a persistent page.</summary>
public enum PageType : ushort
{
    Unknown = 0,
    DatabaseHeader = 1,
    Catalog = 2,
    Heap = 3,
    BPlusTreeInternal = 4,
    BPlusTreeLeaf = 5,
    Overflow = 6,
    Free = 7
}

/// <summary>Version of a page's binary layout.</summary>
public readonly record struct PageFormatVersion(ushort Value)
{
    public static PageFormatVersion Current => new(1);
}

/// <summary>Algorithm used to protect a complete page from corruption.</summary>
public enum PageChecksumAlgorithm : ushort
{
    None = 0,
    Crc32 = 1
}

/// <summary>The fixed metadata prefix shared by every page.</summary>
public readonly record struct PageHeader(
    PageId PageId,
    PageType PageType,
    PageFormatVersion FormatVersion,
    LogSequenceNumber PageLogSequenceNumber,
    PageChecksumAlgorithm ChecksumAlgorithm,
    uint Checksum)
{
    /// <summary>Validates identity, type, version, and checksum algorithm metadata.</summary>
    public void Validate(PageId expectedPageId, PageType? expectedType = null)
    {
        if (PageId != expectedPageId)
            throw new StorageCorruptionException($"Expected {expectedPageId}, found {PageId}.");
        if (!Enum.IsDefined(PageType) || PageType == PageType.Unknown)
            throw new StorageFormatException($"Unsupported page type value {(ushort)PageType}.");
        if (expectedType is not null && PageType != expectedType)
            throw new StorageFormatException($"Expected page type {expectedType}, found {PageType}.");
        if (FormatVersion != PageFormatVersion.Current)
            throw new StorageFormatException($"Unsupported page format version {FormatVersion.Value}.");
        if (ChecksumAlgorithm != PageChecksumAlgorithm.Crc32)
            throw new StorageFormatException($"Unsupported checksum algorithm {(ushort)ChecksumAlgorithm}.");
    }
}

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

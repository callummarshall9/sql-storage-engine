using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

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

using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

/// <summary>Persistent state of one heap slot.</summary>
public enum HeapSlotState : ushort
{
    Unused = 0,
    Live = 1,
    Deleted = 2
}

/// <summary>Heap-specific metadata following the common page header.</summary>
public readonly record struct HeapPageHeader(
    PageId? PreviousPageId,
    PageId? NextPageId,
    ushort SlotCount,
    uint SlotDirectoryEnd,
    uint RowDataStart);

/// <summary>Persistent address, size, state, and stale-reference generation for one slot.</summary>
public readonly record struct HeapSlotEntry(
    HeapSlotState State,
    uint Offset,
    uint Length,
    SlotGeneration Generation);

/// <summary>Encodes and validates the version-one slotted heap-page layout.</summary>
public static class HeapPageLayout
{
    public const int HeaderLength = 64;
    public const int SlotEntryLength = 16;
    public const int PreviousPageOffset = 32;
    public const int NextPageOffset = 41;
    public const int SlotCountOffset = 50;
    public const int SlotDirectoryEndOffset = 52;
    public const int RowDataStartOffset = 56;

    /// <summary>Initializes an empty heap page with valid common and heap headers.</summary>
    public static void Initialize(Span<byte> page, PageId pageId, PageId? previousPageId = null,
        PageId? nextPageId = null)
    {
        ValidatePageSize(page.Length);
        page.Clear();
        PageHeaderCodec.Write(page, new PageHeader(pageId, PageType.Heap, PageFormatVersion.Current,
            new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        WriteOptionalPage(page[PreviousPageOffset..], previousPageId);
        WriteOptionalPage(page[NextPageOffset..], nextPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(page[SlotCountOffset..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(page[SlotDirectoryEndOffset..], HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(page[RowDataStartOffset..], checked((uint)page.Length));
        PageChecksum.WriteChecksum(page, page.Length);
    }

    /// <summary>Reads and fully validates heap metadata and every slot before row access.</summary>
    public static HeapPageHeader ReadHeader(ReadOnlySpan<byte> page, PageId? expectedPageId = null)
    {
        ValidatePageSize(page.Length);
        var common = PageHeaderCodec.Read(page);
        common.Validate(expectedPageId ?? common.PageId, PageType.Heap);
        if (ContainsNonZero(page[60..HeaderLength]))
            throw new StorageFormatException("Reserved heap-header bytes must be zero.");
        var header = new HeapPageHeader(
            ReadOptionalPage(page[PreviousPageOffset..]),
            ReadOptionalPage(page[NextPageOffset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(page[SlotCountOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(page[SlotDirectoryEndOffset..]),
            BinaryPrimitives.ReadUInt32LittleEndian(page[RowDataStartOffset..]));
        ValidateHeader(page, header);
        return header;
    }

    public static HeapSlotEntry ReadSlot(ReadOnlySpan<byte> page, SlotId slotId)
    {
        var header = ReadHeader(page);
        if (slotId.Value >= header.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot {slotId.Value} is outside the slot directory.");
        return ReadAndValidateSlot(page, slotId.Value, header);
    }

    internal static void WriteHeader(Span<byte> page, HeapPageHeader header)
    {
        WriteOptionalPage(page[PreviousPageOffset..], header.PreviousPageId);
        WriteOptionalPage(page[NextPageOffset..], header.NextPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(page[SlotCountOffset..], header.SlotCount);
        BinaryPrimitives.WriteUInt32LittleEndian(page[SlotDirectoryEndOffset..], header.SlotDirectoryEnd);
        BinaryPrimitives.WriteUInt32LittleEndian(page[RowDataStartOffset..], header.RowDataStart);
    }

    internal static void WriteSlot(Span<byte> page, ushort slotIndex, HeapSlotEntry slot)
    {
        var destination = page.Slice(GetSlotOffset(slotIndex), SlotEntryLength);
        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)slot.State);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], slot.Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], slot.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], slot.Generation.Value);
    }

    internal static int GetSlotOffset(ushort slotIndex) => checked(HeaderLength + slotIndex * SlotEntryLength);

    private static void ValidateHeader(ReadOnlySpan<byte> page, HeapPageHeader header)
    {
        var expectedDirectoryEnd = checked((uint)(HeaderLength + header.SlotCount * SlotEntryLength));
        if (header.SlotDirectoryEnd != expectedDirectoryEnd)
            throw new StorageCorruptionException("Heap slot-directory boundary does not match its slot count.");
        if (header.SlotDirectoryEnd > header.RowDataStart || header.RowDataStart > page.Length)
            throw new StorageCorruptionException("Heap slot and row regions overlap or exceed the page.");
        for (ushort slotIndex = 0; slotIndex < header.SlotCount; slotIndex++)
            _ = ReadAndValidateSlot(page, slotIndex, header);
    }

    private static HeapSlotEntry ReadAndValidateSlot(ReadOnlySpan<byte> page, ushort slotIndex, HeapPageHeader header)
    {
        var source = page.Slice(GetSlotOffset(slotIndex), SlotEntryLength);
        var state = (HeapSlotState)BinaryPrimitives.ReadUInt16LittleEndian(source);
        if (!Enum.IsDefined(state)) throw new StorageFormatException($"Unknown heap slot state {(ushort)state}.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(source[2..]) != 0)
            throw new StorageFormatException("Reserved slot bytes must be zero.");
        var entry = new HeapSlotEntry(state, BinaryPrimitives.ReadUInt32LittleEndian(source[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[8..]),
            new SlotGeneration(BinaryPrimitives.ReadUInt32LittleEndian(source[12..])));
        if (state == HeapSlotState.Live)
        {
            var end = checked((ulong)entry.Offset + entry.Length);
            if (entry.Length == 0 || entry.Offset < header.RowDataStart || end > (ulong)page.Length)
                throw new StorageCorruptionException($"Live slot {slotIndex} has an invalid row range.");
        }
        else if (entry.Offset != 0 || entry.Length != 0)
        {
            throw new StorageCorruptionException($"Non-live slot {slotIndex} must not reference row bytes.");
        }
        return entry;
    }

    private static void ValidatePageSize(int length)
    {
        if (!PageConstants.IsSupportedSize(length))
            throw new ArgumentException("Heap pages must use a supported complete page size.", nameof(length));
    }

    private static void WriteOptionalPage(Span<byte> destination, PageId? pageId)
    {
        destination[0] = pageId.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64LittleEndian(destination[1..], pageId.GetValueOrDefault().Value);
    }

    private static PageId? ReadOptionalPage(ReadOnlySpan<byte> source) => source[0] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source[1..]) == 0 => null,
        1 => new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source[1..])),
        _ => throw new StorageFormatException("Invalid nullable heap-page link encoding.")
    };

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value != 0) return true;
        return false;
    }
}

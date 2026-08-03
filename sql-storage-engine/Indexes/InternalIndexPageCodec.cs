using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public sealed record InternalIndexPage(
    PageId PageId,
    PageId? ParentPageId,
    IReadOnlyList<IndexKey> Separators,
    IReadOnlyList<PageId> Children);

/// <summary>Encodes version-one internal B+ tree pages with backward-packed keys.</summary>
public static class InternalIndexPageCodec
{
    public const int HeaderLength = 64;
    public const int SlotLength = 16;
    public const int ParentOffset = 32;
    public const int SeparatorCountOffset = 41;
    public const int ChildCountOffset = 43;
    public const int SlotDirectoryEndOffset = 45;
    public const int KeyDataStartOffset = 49;
    public const int FirstChildOffset = 56;

    public static bool CanFit(int pageSize, IEnumerable<IndexKey> separators)
    {
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var keys = separators.ToArray();
        var required = checked(HeaderLength + keys.Length * SlotLength + keys.Sum(key => key.Bytes.Length));
        return keys.Length is > 0 and <= ushort.MaxValue && required <= pageSize;
    }

    public static void Write(Span<byte> page, InternalIndexPage model)
    {
        ValidateModel(model);
        if (!CanFit(page.Length, model.Separators)) throw new ArgumentException("Internal node does not fit on the page.", nameof(model));
        page.Clear();
        PageHeaderCodec.Write(page, new PageHeader(model.PageId, PageType.BPlusTreeInternal,
            PageFormatVersion.Current, new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        WriteOptionalPage(page[ParentOffset..], model.ParentPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(page[SeparatorCountOffset..], checked((ushort)model.Separators.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(page[ChildCountOffset..], checked((ushort)model.Children.Count));
        var directoryEnd = checked(HeaderLength + model.Separators.Count * SlotLength);
        BinaryPrimitives.WriteUInt32LittleEndian(page[SlotDirectoryEndOffset..], checked((uint)directoryEnd));
        BinaryPrimitives.WriteUInt64LittleEndian(page[FirstChildOffset..], model.Children[0].Value);
        var cursor = page.Length;
        for (var index = 0; index < model.Separators.Count; index++)
        {
            var key = model.Separators[index].Bytes.Span;
            cursor = checked(cursor - key.Length);
            key.CopyTo(page[cursor..]);
            var slot = page.Slice(HeaderLength + index * SlotLength, SlotLength);
            BinaryPrimitives.WriteUInt32LittleEndian(slot, checked((uint)cursor));
            BinaryPrimitives.WriteUInt16LittleEndian(slot[4..], checked((ushort)key.Length));
            BinaryPrimitives.WriteUInt64LittleEndian(slot[8..], model.Children[index + 1].Value);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(page[KeyDataStartOffset..], checked((uint)cursor));
        PageChecksum.WriteChecksum(page, page.Length);
    }

    public static InternalIndexPage Read(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        if (!PageConstants.IsSupportedSize(page.Length)) throw new ArgumentException("Index page has an unsupported size.", nameof(page));
        PageChecksum.ValidateChecksum(page, page.Length);
        PageHeaderCodec.Read(page).Validate(expectedPageId, PageType.BPlusTreeInternal);
        if (ContainsNonZero(page[53..56])) throw new StorageFormatException("Reserved internal-header bytes must be zero.");
        var parent = ReadOptionalPage(page[ParentOffset..]);
        var separatorCount = BinaryPrimitives.ReadUInt16LittleEndian(page[SeparatorCountOffset..]);
        var childCount = BinaryPrimitives.ReadUInt16LittleEndian(page[ChildCountOffset..]);
        if (separatorCount == 0 || childCount != separatorCount + 1)
            throw new StorageCorruptionException("Internal child count must equal separator count plus one.");
        var directoryEnd = BinaryPrimitives.ReadUInt32LittleEndian(page[SlotDirectoryEndOffset..]);
        var expectedDirectoryEnd = checked((uint)(HeaderLength + separatorCount * SlotLength));
        var keyStart = BinaryPrimitives.ReadUInt32LittleEndian(page[KeyDataStartOffset..]);
        if (directoryEnd != expectedDirectoryEnd || directoryEnd > keyStart || keyStart > page.Length)
            throw new StorageCorruptionException("Internal slot and key regions overlap or exceed the page.");
        List<PageId> children = [ReadChild(BinaryPrimitives.ReadUInt64LittleEndian(page[FirstChildOffset..]), expectedPageId)];
        List<IndexKey> keys = [];
        var expectedKeyEnd = page.Length;
        for (var index = 0; index < separatorCount; index++)
        {
            var slot = page.Slice(HeaderLength + index * SlotLength, SlotLength);
            if (BinaryPrimitives.ReadUInt16LittleEndian(slot[6..]) != 0)
                throw new StorageFormatException("Reserved internal-slot bytes must be zero.");
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(slot);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(slot[4..]);
            if (length == 0 || checked((ulong)offset + length) > (ulong)page.Length ||
                offset < keyStart || checked((int)(offset + length)) != expectedKeyEnd)
                throw new StorageCorruptionException("Internal key offset is malformed.");
            var key = new IndexKey(page.Slice(checked((int)offset), length));
            if (keys.Count > 0 && keys[^1].CompareTo(key) > 0)
                throw new StorageCorruptionException("Internal separators are not ordered.");
            keys.Add(key);
            expectedKeyEnd = checked((int)offset);
            children.Add(ReadChild(BinaryPrimitives.ReadUInt64LittleEndian(slot[8..]), expectedPageId));
        }
        if (expectedKeyEnd != keyStart || children.Distinct().Count() != children.Count)
            throw new StorageCorruptionException("Internal key packing or child IDs are invalid.");
        var model = new InternalIndexPage(expectedPageId, parent, keys.AsReadOnly(), children.AsReadOnly());
        ValidateModel(model);
        return model;
    }

    private static void ValidateModel(InternalIndexPage model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.PageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(model));
        if (model.ParentPageId?.Value == 0 || model.ParentPageId == model.PageId) throw new ArgumentException("Invalid parent page ID.", nameof(model));
        if (model.Separators.Count == 0 || model.Children.Count != model.Separators.Count + 1)
            throw new ArgumentException("Internal child count must equal nonzero separator count plus one.", nameof(model));
        for (var index = 1; index < model.Separators.Count; index++)
            if (model.Separators[index - 1].CompareTo(model.Separators[index]) > 0)
                throw new ArgumentException("Separators must be ordered.", nameof(model));
        if (model.Children.Any(child => child.Value == 0 || child == model.PageId) || model.Children.Distinct().Count() != model.Children.Count)
            throw new ArgumentException("Child page IDs must be unique, nonzero, and not self-referential.", nameof(model));
    }

    private static PageId ReadChild(ulong value, PageId pageId)
    {
        var child = new PageId(value);
        if (child.Value == 0 || child == pageId) throw new StorageCorruptionException("Invalid internal child page ID.");
        return child;
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
        _ => throw new StorageFormatException("Invalid nullable internal parent encoding.")
    };
    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value != 0) return true;
        return false;
    }
}

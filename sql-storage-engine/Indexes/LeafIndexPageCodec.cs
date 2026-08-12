using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public readonly record struct LeafIndexEntry
{
    private readonly byte[] _payload;
    public LeafIndexEntry(IndexKey key, RowId rowId) : this(key, rowId, ReadOnlyMemory<byte>.Empty) { }
    public LeafIndexEntry(IndexKey key, RowId rowId, ReadOnlyMemory<byte> includedPayload)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        RowId = rowId;
        _payload = includedPayload.ToArray();
    }
    public IndexKey Key { get; }
    public RowId RowId { get; }
    public ReadOnlyMemory<byte> IncludedPayload => _payload ?? [];
    public bool Equals(LeafIndexEntry other) => Key.Equals(other.Key) && RowId == other.RowId &&
        IncludedPayload.Span.SequenceEqual(other.IncludedPayload.Span);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key); hash.Add(RowId);
        foreach (var value in IncludedPayload.Span) hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record LeafIndexPage(
    PageId PageId,
    PageId? ParentPageId,
    PageId? PreviousPageId,
    PageId? NextPageId,
    IReadOnlyList<LeafIndexEntry> Entries);

/// <summary>Encodes version-one linked B+ tree leaf pages.</summary>
public static class LeafIndexPageCodec
{
    public const int HeaderLength = 72;
    public const int SlotLength = 24;
    public const int ParentOffset = 32;
    public const int PreviousOffset = 41;
    public const int NextOffset = 50;
    public const int EntryCountOffset = 59;
    public const int SlotDirectoryEndOffset = 61;
    public const int KeyDataStartOffset = 65;

    public static bool CanFit(int pageSize, IEnumerable<LeafIndexEntry> entries)
    {
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var values = entries.ToArray();
        var required = checked(HeaderLength + values.Length * SlotLength +
            values.Sum(entry => entry.Key.Bytes.Length + entry.IncludedPayload.Length));
        return values.Length <= ushort.MaxValue && required <= pageSize;
    }

    public static void Write(Span<byte> page, LeafIndexPage model)
    {
        ValidateModel(model);
        if (!CanFit(page.Length, model.Entries)) throw new ArgumentException("Leaf entries do not fit on the page.", nameof(model));
        page.Clear();
        PageHeaderCodec.Write(page, new PageHeader(model.PageId, PageType.BPlusTreeLeaf,
            PageFormatVersion.Current, new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        WriteOptionalPage(page[ParentOffset..], model.ParentPageId);
        WriteOptionalPage(page[PreviousOffset..], model.PreviousPageId);
        WriteOptionalPage(page[NextOffset..], model.NextPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(page[EntryCountOffset..], checked((ushort)model.Entries.Count));
        var directoryEnd = checked(HeaderLength + model.Entries.Count * SlotLength);
        BinaryPrimitives.WriteUInt32LittleEndian(page[SlotDirectoryEndOffset..], checked((uint)directoryEnd));
        page[69] = 1; // Layout one uses the formerly reserved slot fields for a covered-row payload.
        var cursor = page.Length;
        for (var index = 0; index < model.Entries.Count; index++)
        {
            var entry = model.Entries[index];
            var payload = entry.IncludedPayload.Span;
            var payloadOffset = 0;
            if (!payload.IsEmpty)
            {
                cursor = checked(cursor - payload.Length);
                payload.CopyTo(page[cursor..]);
                payloadOffset = cursor;
            }
            var key = entry.Key.Bytes.Span;
            cursor = checked(cursor - key.Length);
            key.CopyTo(page[cursor..]);
            var slot = page.Slice(HeaderLength + index * SlotLength, SlotLength);
            BinaryPrimitives.WriteUInt32LittleEndian(slot, checked((uint)cursor));
            BinaryPrimitives.WriteUInt16LittleEndian(slot[4..], checked((ushort)key.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(slot[6..], checked((ushort)payload.Length));
            BinaryPrimitives.WriteUInt64LittleEndian(slot[8..], entry.RowId.PageId.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(slot[16..], entry.RowId.SlotId.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(slot[18..], checked((ushort)payloadOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(slot[20..], entry.RowId.Generation.Value);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(page[KeyDataStartOffset..], checked((uint)cursor));
        PageChecksum.WriteChecksum(page, page.Length);
    }

    public static LeafIndexPage Read(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        if (!PageConstants.IsSupportedSize(page.Length)) throw new ArgumentException("Index page has an unsupported size.", nameof(page));
        PageChecksum.ValidateChecksum(page, page.Length);
        PageHeaderCodec.Read(page).Validate(expectedPageId, PageType.BPlusTreeLeaf);
        var layout = page[69];
        if (layout > 1 || ContainsNonZero(page[70..HeaderLength]))
            throw new StorageFormatException("Unknown leaf payload layout or nonzero reserved bytes.");
        var parent = ReadOptionalPage(page[ParentOffset..]);
        var previous = ReadOptionalPage(page[PreviousOffset..]);
        var next = ReadOptionalPage(page[NextOffset..]);
        if (parent == expectedPageId || previous == expectedPageId || next == expectedPageId ||
            previous is not null && previous == next)
            throw new StorageCorruptionException("Leaf page links are self-referential or ambiguous.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(page[EntryCountOffset..]);
        var directoryEnd = BinaryPrimitives.ReadUInt32LittleEndian(page[SlotDirectoryEndOffset..]);
        var expectedDirectoryEnd = checked((uint)(HeaderLength + count * SlotLength));
        var keyStart = BinaryPrimitives.ReadUInt32LittleEndian(page[KeyDataStartOffset..]);
        if (directoryEnd != expectedDirectoryEnd || directoryEnd > keyStart || keyStart > page.Length)
            throw new StorageCorruptionException("Leaf slot and key regions overlap or exceed the page.");
        List<LeafIndexEntry> entries = [];
        var expectedKeyEnd = page.Length;
        for (var index = 0; index < count; index++)
        {
            var slot = page.Slice(HeaderLength + index * SlotLength, SlotLength);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(slot);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(slot[4..]);
            var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(slot[6..]);
            var payloadOffset = BinaryPrimitives.ReadUInt16LittleEndian(slot[18..]);
            if (layout == 0 && (payloadLength != 0 || payloadOffset != 0))
                throw new StorageFormatException("Legacy leaf slots contain nonzero reserved bytes.");
            var payloadEnd = payloadLength == 0 ? expectedKeyEnd : checked(payloadOffset + payloadLength);
            if (payloadLength != 0 && (payloadOffset < keyStart || payloadEnd != expectedKeyEnd))
                throw new StorageCorruptionException("Leaf included payload offset is malformed.");
            var expectedKeyBoundary = payloadLength == 0 ? expectedKeyEnd : payloadOffset;
            if (length == 0 || checked((ulong)offset + length) > (ulong)page.Length || offset < keyStart ||
                checked((int)(offset + length)) != expectedKeyBoundary)
                throw new StorageCorruptionException("Leaf key offset is malformed.");
            var key = new IndexKey(page.Slice(checked((int)offset), length));
            if (entries.Count > 0 && entries[^1].Key.CompareTo(key) > 0)
                throw new StorageCorruptionException("Leaf entries are not key ordered.");
            var rowPageId = new PageId(BinaryPrimitives.ReadUInt64LittleEndian(slot[8..]));
            if (rowPageId.Value == 0) throw new StorageCorruptionException("Leaf RowId cannot reference page zero.");
            entries.Add(new LeafIndexEntry(key, new RowId(rowPageId,
                new SlotId(BinaryPrimitives.ReadUInt16LittleEndian(slot[16..])),
                new SlotGeneration(BinaryPrimitives.ReadUInt32LittleEndian(slot[20..]))),
                payloadLength == 0 ? ReadOnlyMemory<byte>.Empty : page.Slice(payloadOffset, payloadLength).ToArray()));
            expectedKeyEnd = checked((int)offset);
        }
        if (expectedKeyEnd != keyStart) throw new StorageCorruptionException("Leaf key packing is invalid.");
        var model = new LeafIndexPage(expectedPageId, parent, previous, next, entries.AsReadOnly());
        ValidateModel(model);
        return model;
    }

    private static void ValidateModel(LeafIndexPage model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.PageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(model));
        if (new[] { model.ParentPageId, model.PreviousPageId, model.NextPageId }
            .Any(link => link?.Value == 0 || link == model.PageId))
            throw new ArgumentException("Leaf links cannot reference page zero or self.", nameof(model));
        if (model.PreviousPageId is not null && model.PreviousPageId == model.NextPageId)
            throw new ArgumentException("Previous and next links cannot be equal.", nameof(model));
        for (var index = 1; index < model.Entries.Count; index++)
            if (model.Entries[index - 1].Key.CompareTo(model.Entries[index].Key) > 0)
                throw new ArgumentException("Leaf entries must be ordered.", nameof(model));
        if (model.Entries.Any(entry => entry.RowId.PageId.Value == 0))
            throw new ArgumentException("Leaf RowId cannot reference page zero.", nameof(model));
        if (model.Entries.Any(entry => entry.IncludedPayload.Length > ushort.MaxValue))
            throw new ArgumentException("Leaf included payload exceeds 65,535 bytes.", nameof(model));
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
        _ => throw new StorageFormatException("Invalid nullable leaf link encoding.")
    };
    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value != 0) return true;
        return false;
    }
}

using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Heap;

/// <summary>Outcome of replacing a row on its current heap page.</summary>
public enum HeapUpdateResult
{
    Updated,
    Absent,
    RelocationRequired
}

public enum HeapReadResult
{
    Found,
    UnknownSlot,
    Deleted,
    StaleGeneration
}

public readonly record struct HeapPageRow(
    SlotId SlotId,
    SlotGeneration Generation,
    ReadOnlyMemory<byte> Bytes);

/// <summary>Provides validated insertion and lookup operations over one page-owned heap buffer.</summary>
public sealed class HeapPage
{
    private readonly Memory<byte> _page;

    public HeapPage(Memory<byte> page, PageId? expectedPageId = null)
    {
        _ = HeapPageLayout.ReadHeader(page.Span, expectedPageId);
        _page = page;
    }

    public PageId PageId => PageHeader().PageId;
    public HeapPageHeader Header => HeapPageLayout.ReadHeader(_page.Span, PageId);
    public PageId? NextPageId => Header.NextPageId;
    public int FreeBytes
    {
        get
        {
            var header = HeapPageLayout.ReadHeader(_page.Span, PageId);
            return checked((int)(header.RowDataStart - header.SlotDirectoryEnd));
        }
    }

    /// <summary>Inserts a non-empty raw record without partially modifying the page on failure.</summary>
    public bool TryInsert(ReadOnlySpan<byte> row, out SlotId slotId, out SlotGeneration generation)
    {
        if (row.IsEmpty) throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        var page = _page.Span;
        var header = HeapPageLayout.ReadHeader(page, PageId);
        ushort? reusable = null;
        HeapSlotEntry reusableEntry = default;
        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = HeapPageLayout.ReadSlot(page, new SlotId(index));
            if (candidate.State == HeapSlotState.Unused ||
                candidate.State == HeapSlotState.Deleted && candidate.Generation.Value != uint.MaxValue)
            {
                reusable = index;
                reusableEntry = candidate;
                break;
            }
        }

        var slotBytes = reusable.HasValue ? 0 : HeapPageLayout.SlotEntryLength;
        if (!reusable.HasValue && header.SlotCount == ushort.MaxValue || row.Length > FreeBytes - slotBytes)
        {
            slotId = default;
            generation = default;
            return false;
        }

        var rowStart = checked(header.RowDataStart - (uint)row.Length);
        var slotIndex = reusable ?? header.SlotCount;
        generation = reusableEntry.State == HeapSlotState.Deleted
            ? new SlotGeneration(checked(reusableEntry.Generation.Value + 1))
            : reusableEntry.Generation;
        row.CopyTo(page.Slice(checked((int)rowStart), row.Length));
        HeapPageLayout.WriteSlot(page, slotIndex,
            new HeapSlotEntry(HeapSlotState.Live, rowStart, checked((uint)row.Length), generation));
        if (!reusable.HasValue)
        {
            header = header with
            {
                SlotCount = checked((ushort)(header.SlotCount + 1)),
                SlotDirectoryEnd = checked(header.SlotDirectoryEnd + HeapPageLayout.SlotEntryLength)
            };
        }
        header = header with { RowDataStart = rowStart };
        HeapPageLayout.WriteHeader(page, header);
        slotId = new SlotId(slotIndex);
        return true;
    }

    /// <summary>Deletes a matching live slot; absent, deleted, and stale slots return false.</summary>
    public bool Delete(SlotId slotId, SlotGeneration generation)
    {
        var page = _page.Span;
        var header = HeapPageLayout.ReadHeader(page, PageId);
        if (slotId.Value >= header.SlotCount) return false;
        var slot = HeapPageLayout.ReadSlot(page, slotId);
        if (slot.State != HeapSlotState.Live || slot.Generation != generation) return false;
        HeapPageLayout.WriteSlot(page, slotId.Value,
            new HeapSlotEntry(HeapSlotState.Deleted, 0, 0, slot.Generation));
        return true;
    }

    /// <summary>Replaces a matching row, compacting size changes when they fit on the current page.</summary>
    public HeapUpdateResult Update(SlotId slotId, SlotGeneration generation, ReadOnlySpan<byte> row)
    {
        if (row.IsEmpty) throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        var page = _page.Span;
        var header = HeapPageLayout.ReadHeader(page, PageId);
        if (slotId.Value >= header.SlotCount) return HeapUpdateResult.Absent;
        var slot = HeapPageLayout.ReadSlot(page, slotId);
        if (slot.State != HeapSlotState.Live || slot.Generation != generation) return HeapUpdateResult.Absent;
        if (slot.Length == row.Length)
        {
            row.CopyTo(page.Slice(checked((int)slot.Offset), row.Length));
            return HeapUpdateResult.Updated;
        }

        ulong liveBytes = 0;
        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = HeapPageLayout.ReadSlot(page, new SlotId(index));
            if (candidate.State == HeapSlotState.Live) liveBytes = checked(liveBytes + candidate.Length);
        }
        var bytesAfterUpdate = checked(liveBytes - slot.Length + (uint)row.Length);
        var payloadCapacity = checked((ulong)page.Length - header.SlotDirectoryEnd);
        if (bytesAfterUpdate > payloadCapacity) return HeapUpdateResult.RelocationRequired;

        var rows = new List<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)>();
        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = HeapPageLayout.ReadSlot(page, new SlotId(index));
            if (candidate.State != HeapSlotState.Live) continue;
            var bytes = index == slotId.Value
                ? row.ToArray()
                : page.Slice(checked((int)candidate.Offset), checked((int)candidate.Length)).ToArray();
            rows.Add((index, candidate with { Length = checked((uint)bytes.Length) }, bytes));
        }
        Repack(page, header, rows);
        return HeapUpdateResult.Updated;
    }

    /// <summary>Deterministically packs all live row bytes without changing slot identity.</summary>
    public void Compact()
    {
        var page = _page.Span;
        var header = HeapPageLayout.ReadHeader(page, PageId);
        var liveRows = new List<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)>();
        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var slot = HeapPageLayout.ReadSlot(page, new SlotId(index));
            if (slot.State == HeapSlotState.Live)
            {
                liveRows.Add((index, slot,
                    page.Slice(checked((int)slot.Offset), checked((int)slot.Length)).ToArray()));
            }
        }

        Repack(page, header, liveRows);
    }

    /// <summary>Returns a defensive copy of a live row when slot and generation match.</summary>
    public bool TryRead(SlotId slotId, SlotGeneration generation, out ReadOnlyMemory<byte> row)
        => Read(slotId, generation, out row) == HeapReadResult.Found;

    /// <summary>Reads a defensive row copy while distinguishing slot absence, deletion, and staleness.</summary>
    public HeapReadResult Read(SlotId slotId, SlotGeneration generation, out ReadOnlyMemory<byte> row)
    {
        var page = _page.Span;
        var header = HeapPageLayout.ReadHeader(page, PageId);
        if (slotId.Value >= header.SlotCount)
        {
            row = default;
            return HeapReadResult.UnknownSlot;
        }
        var slot = HeapPageLayout.ReadSlot(page, slotId);
        if (slot.State != HeapSlotState.Live)
        {
            row = default;
            return HeapReadResult.Deleted;
        }
        if (slot.Generation != generation)
        {
            row = default;
            return HeapReadResult.StaleGeneration;
        }
        row = page.Slice(checked((int)slot.Offset), checked((int)slot.Length)).ToArray();
        return HeapReadResult.Found;
    }

    internal void SetNextPage(PageId? nextPageId)
    {
        if (nextPageId == PageId) throw new ArgumentException("A heap page cannot link to itself.", nameof(nextPageId));
        var header = Header;
        HeapPageLayout.WriteHeader(_page.Span, header with { NextPageId = nextPageId });
    }

    /// <summary>Copies all live rows in ascending slot order.</summary>
    public IReadOnlyList<HeapPageRow> ReadLiveRows()
    {
        var header = Header;
        var rows = new List<HeapPageRow>();
        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var slotId = new SlotId(index);
            var slot = HeapPageLayout.ReadSlot(_page.Span, slotId);
            if (slot.State != HeapSlotState.Live) continue;
            var bytes = _page.Span.Slice(checked((int)slot.Offset), checked((int)slot.Length)).ToArray();
            rows.Add(new HeapPageRow(slotId, slot.Generation, bytes));
        }
        return rows;
    }

    private Pages.PageHeader PageHeader() => Pages.PageHeaderCodec.Read(_page.Span);

    private static void Repack(Span<byte> page, HeapPageHeader header,
        IEnumerable<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)> liveRows)
    {
        page[checked((int)header.SlotDirectoryEnd)..].Clear();
        var cursor = checked((uint)page.Length);
        foreach (var live in liveRows)
        {
            cursor = checked(cursor - live.Entry.Length);
            live.Bytes.CopyTo(page.Slice(checked((int)cursor), live.Bytes.Length));
            HeapPageLayout.WriteSlot(page, live.SlotIndex, live.Entry with { Offset = cursor });
        }
        HeapPageLayout.WriteHeader(page, header with { RowDataStart = cursor });
    }
}

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
internal sealed partial class HeapPageLayoutOrchestrationService
{
    public HeapPageLayout BindHeapPageLayout(Memory<byte> page, ulong? expectedPageId) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageLayoutBinding(page: page, expectedPageId: expectedPageId);

        return ReadCore(page: page.Span, expectedPageId: expectedPageId)with
        {
            Page = page
        };
    });

    public HeapPageLayout ReadHeapPageLayoutState(HeapPageLayout heapPageLayout) =>
        TryCatch(operation: () =>
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);

        return ReadCore(page: heapPageLayout.Page.Span, expectedPageId: heapPageLayout.CommonHeader.PageId.Value)with
        {
            Page = heapPageLayout.Page
        };
    });

    public int CountHeapPageLayoutFreeBytes(HeapPageLayout heapPageLayout) =>
        TryCatch(operation: () =>
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        return ReadFreeBytesCore(heapPageLayout: heapPageLayout);
    });

    public int CountHeapPageLayoutLiveRows(HeapPageLayout heapPageLayout) =>
        TryCatch(operation: () =>
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        return CountLiveRowsCore(heapPageLayout: heapPageLayout);
    });

    public HeapPageLayout TryInsertHeapPageLayoutRow(HeapPageLayout heapPageLayout, ReadOnlySpan<byte> row) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutRowOnInsert(heapPageLayout: context.heapPageLayout, row: context.row);
        var inserted = TryInsertCore(heapPageLayout: context.heapPageLayout, row: context.row, slotId: out var slotId, generation: out var generation);

        return context.heapPageLayout with
        {
            Inserted = inserted,
            ResultSlotId = slotId,
            ResultGeneration = generation
        };
    }, context: new HeapPageRowMutationContext { heapPageLayout = heapPageLayout, row = row });

    public bool DeleteHeapPageLayoutRow(HeapPageLayout deletedHeapPageLayout, ushort slotIndex, uint generation) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageLayoutRowOnDelete(heapPageLayout: deletedHeapPageLayout, slotIndex: slotIndex, generation: generation);
        return DeleteCore(deletedHeapPageLayout: deletedHeapPageLayout, deletedSlotId: new SlotId { Value = slotIndex }, deletedSlotGeneration: new SlotGeneration { Value = generation });
    });

    public HeapUpdateResult UpdateHeapPageLayoutRow(HeapPageLayout updatedHeapPageLayout, ushort slotIndex, uint generation, ReadOnlySpan<byte> row) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutRowOnUpdate(heapPageLayout: context.updatedHeapPageLayout, slotIndex: context.slotIndex, generation: context.generation, row: context.row);
        return UpdateCore(updatedHeapPageLayout: context.updatedHeapPageLayout, updatedSlotId: new SlotId { Value = context.slotIndex }, updatedSlotGeneration: new SlotGeneration { Value = context.generation }, row: context.row);
    }, context: new HeapPageRowMutationContext { updatedHeapPageLayout = updatedHeapPageLayout, slotIndex = slotIndex, generation = generation, row = row });

    public void CompactHeapPageLayout(HeapPageLayout heapPageLayout) =>
        TryCatch(operation: () =>
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        CompactCore(heapPageLayout: heapPageLayout);
    });

    public HeapPageLayout ReadHeapPageLayoutRow(HeapPageLayout heapPageLayout, ushort slotIndex, uint generation) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageLayoutSelection(heapPageLayout: heapPageLayout, slotIndex: slotIndex, generation: generation);
        var result = ReadRowCore(heapPageLayout: heapPageLayout, slotId: new SlotId { Value = slotIndex }, generation: new SlotGeneration { Value = generation }, row: out var row);

        return heapPageLayout with
        {
            ReadResult = result,
            Row = row
        };
    });

    public void LinkHeapPageLayout(HeapPageLayout heapPageLayout, ulong? nextPageId) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageLayoutLink(heapPageLayout: heapPageLayout, nextPageId: nextPageId);
        SetNextPageCore(heapPageLayout: heapPageLayout, nextPageId: nextPageId is null ? null : new PageId { Value = nextPageId.Value });
    });

    public HeapPageLayout ReadHeapPageLayoutLiveRows(HeapPageLayout heapPageLayout) =>
        TryCatch(operation: () =>
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);

        return heapPageLayout with
        {
            LiveRows = ReadLiveRowsCore(heapPageLayout: heapPageLayout)
        };
    });

    private static bool TryInsertCore(HeapPageLayout heapPageLayout, ReadOnlySpan<byte> row, out SlotId slotId, out SlotGeneration generation)
    {
        if (row.IsEmpty)
        {
            throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        }

        var page = heapPageLayout.Page.Span;

        var header = ReadHeapPageHeaderCore(page: page, expectedPageId: (ReadCommonHeaderCore(heapPageLayout: heapPageLayout)
            .PageId).Value);

        ushort? reusable = null;
        HeapSlotEntry reusableEntry = default;

        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = ReadHeapSlotEntryCore(page: page, slotIndex: (new sql_storage_engine.Models.Identifiers.SlotId { Value = index }).Value);

            if (candidate.State == HeapSlotState.Unused || candidate.State == HeapSlotState.Deleted && candidate.Generation.Value != uint.MaxValue)
            {
                reusable = index;
                reusableEntry = candidate;
                break;
            }
        }

        var slotBytes = reusable.HasValue ? 0 : sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength;

        if (!reusable.HasValue && header.SlotCount == ushort.MaxValue || row.Length > ReadFreeBytesCore(heapPageLayout: heapPageLayout) - slotBytes)
        {
            slotId = default;
            generation = default;
            return false;
        }

        var rowStart = checked(header.RowDataStart - (uint)row.Length);
        var slotIndex = reusable ?? header.SlotCount;

        generation = reusableEntry.State == HeapSlotState.Deleted ? new sql_storage_engine.Models.Identifiers.SlotGeneration
        {
            Value = checked(reusableEntry.Generation.Value + 1)
        }

        : reusableEntry.Generation;

        row.CopyTo(destination: page.Slice(start: checked((int)rowStart), length: row.Length));
        slots.WriteHeapSlotEntry(page: page, slotIndex: slotIndex, heapSlotEntry: new HeapSlotEntry { State = HeapSlotState.Live, Offset = rowStart, Length = checked((uint)row.Length), Generation = generation });

        if (!reusable.HasValue)
        {
            header = header with
            {
                SlotCount = checked((ushort)(header.SlotCount + 1)),
                SlotDirectoryEnd = checked(header.SlotDirectoryEnd + sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength)
            };
        }

        header = header with
        {
            RowDataStart = rowStart
        };

        heapHeaders.WriteHeapPageHeader(page: page, heapPageHeader: header);

        slotId = new sql_storage_engine.Models.Identifiers.SlotId
        {
            Value = slotIndex
        };

        return true;
    }

    private static bool DeleteCore(HeapPageLayout deletedHeapPageLayout, SlotId deletedSlotId, SlotGeneration deletedSlotGeneration)
    {
        var page = deletedHeapPageLayout.Page.Span;

        var header = ReadHeapPageHeaderCore(page: page, expectedPageId: (ReadCommonHeaderCore(heapPageLayout: deletedHeapPageLayout)
            .PageId).Value);

        if (deletedSlotId.Value >= header.SlotCount)
        {
            return false;
        }

        var slot = ReadHeapSlotEntryCore(page: page, slotIndex: (deletedSlotId).Value);

        if (slot.State != HeapSlotState.Live || slot.Generation != deletedSlotGeneration)
        {
            return false;
        }

        slots.WriteHeapSlotEntry(page: page, slotIndex: deletedSlotId.Value, heapSlotEntry: new HeapSlotEntry { State = HeapSlotState.Deleted, Offset = 0, Length = 0, Generation = slot.Generation });
        return true;
    }

    private static HeapUpdateResult UpdateCore(HeapPageLayout updatedHeapPageLayout, SlotId updatedSlotId, SlotGeneration updatedSlotGeneration, ReadOnlySpan<byte> row)
    {
        if (row.IsEmpty)
        {
            throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        }

        var page = updatedHeapPageLayout.Page.Span;

        var header = ReadHeapPageHeaderCore(page: page, expectedPageId: (ReadCommonHeaderCore(heapPageLayout: updatedHeapPageLayout)
            .PageId).Value);

        if (updatedSlotId.Value >= header.SlotCount)
        {
            return HeapUpdateResult.Absent;
        }

        var slot = ReadHeapSlotEntryCore(page: page, slotIndex: (updatedSlotId).Value);

        if (slot.State != HeapSlotState.Live || slot.Generation != updatedSlotGeneration)
        {
            return HeapUpdateResult.Absent;
        }

        if (slot.Length == row.Length)
        {
            row.CopyTo(destination: page.Slice(start: checked((int)slot.Offset), length: row.Length));
            return HeapUpdateResult.Updated;
        }

        ulong liveBytes = 0;

        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = ReadHeapSlotEntryCore(page: page, slotIndex: (new sql_storage_engine.Models.Identifiers.SlotId { Value = index }).Value);

            if (candidate.State == HeapSlotState.Live)
            {
                liveBytes = checked(liveBytes + candidate.Length);
            }
        }

        var bytesAfterUpdate = checked(liveBytes - slot.Length + (uint)row.Length);
        var payloadCapacity = checked((ulong)page.Length - header.SlotDirectoryEnd);

        if (bytesAfterUpdate > payloadCapacity)
        {
            return HeapUpdateResult.RelocationRequired;
        }

        var rows = new List<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)>();

        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var candidate = ReadHeapSlotEntryCore(page: page, slotIndex: (new sql_storage_engine.Models.Identifiers.SlotId { Value = index }).Value);

            if (candidate.State != HeapSlotState.Live)
            {
                continue;
            }

            var bytes = index == updatedSlotId.Value ? row.ToArray() : page.Slice(start: checked((int)candidate.Offset), length: checked((int)candidate.Length))
            .ToArray();

            rows.Add(item: (index, candidate with { Length = checked((uint)bytes.Length) }, bytes));
        }

        Repack(page: page, header: header, liveRows: rows);
        return HeapUpdateResult.Updated;
    }

    private static void CompactCore(HeapPageLayout heapPageLayout)
    {
        var page = heapPageLayout.Page.Span;

        var header = ReadHeapPageHeaderCore(page: page, expectedPageId: (ReadCommonHeaderCore(heapPageLayout: heapPageLayout)
            .PageId).Value);

        var liveRows = new List<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)>();

        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var slot = ReadHeapSlotEntryCore(page: page, slotIndex: (new sql_storage_engine.Models.Identifiers.SlotId { Value = index }).Value);

            if (slot.State == HeapSlotState.Live)
            {
                liveRows.Add(item: (index, slot, page.Slice(start: checked((int)slot.Offset), length: checked((int)slot.Length))
            .ToArray()));
            }
        }

        Repack(page: page, header: header, liveRows: liveRows);
    }

    private static HeapReadResult ReadRowCore(HeapPageLayout heapPageLayout, SlotId slotId, SlotGeneration generation, out ReadOnlyMemory<byte> row)
    {
        var page = heapPageLayout.Page.Span;

        var header = ReadHeapPageHeaderCore(page: page, expectedPageId: (ReadCommonHeaderCore(heapPageLayout: heapPageLayout)
            .PageId).Value);

        if (slotId.Value >= header.SlotCount)
        {
            row = default;
            return HeapReadResult.UnknownSlot;
        }

        var slot = ReadHeapSlotEntryCore(page: page, slotIndex: (slotId).Value);

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

        row = page.Slice(start: checked((int)slot.Offset), length: checked((int)slot.Length))
            .ToArray();

        return HeapReadResult.Found;
    }

    private static void SetNextPageCore(HeapPageLayout heapPageLayout, PageId? nextPageId)
    {
        if (nextPageId == ReadCommonHeaderCore(heapPageLayout: heapPageLayout)
            .PageId)
        {
            throw new ArgumentException("A heap page cannot link to itself.", nameof(nextPageId));
        }

        var header = ReadHeaderCore(heapPageLayout: heapPageLayout);
        heapHeaders.WriteHeapPageHeader(page: heapPageLayout.Page.Span, heapPageHeader: header with { NextPageId = nextPageId });
    }

    private static IReadOnlyList<HeapPageRow> ReadLiveRowsCore(HeapPageLayout heapPageLayout)
    {
        var header = ReadHeaderCore(heapPageLayout: heapPageLayout);
        var rows = new List<HeapPageRow>();

        for (ushort index = 0; index < header.SlotCount; index++)
        {
            var slotId = new sql_storage_engine.Models.Identifiers.SlotId
            {
                Value = index
            };

            var slot = ReadHeapSlotEntryCore(page: heapPageLayout.Page.Span, slotIndex: (slotId).Value);

            if (slot.State != HeapSlotState.Live)
            {
                continue;
            }

            var bytes = heapPageLayout.Page.Span.Slice(start: checked((int)slot.Offset), length: checked((int)slot.Length))
            .ToArray();

            rows.Add(item: new HeapPageRow { SlotId = slotId, Generation = slot.Generation, Bytes = bytes });
        }

        return rows;
    }

    private static void Repack(Span<byte> page, HeapPageHeader header, IEnumerable<(ushort SlotIndex, HeapSlotEntry Entry, byte[] Bytes)> liveRows)
    {
        page[checked((int)header.SlotDirectoryEnd)..].Clear();
        var cursor = checked((uint)page.Length);

        foreach (var live in liveRows)
        {
            cursor = checked(cursor - live.Entry.Length);
            live.Bytes.CopyTo(destination: page.Slice(start: checked((int)cursor), length: live.Bytes.Length));
            slots.WriteHeapSlotEntry(page: page, slotIndex: live.SlotIndex, heapSlotEntry: live.Entry with { Offset = cursor });
        }

        heapHeaders.WriteHeapPageHeader(page: page, heapPageHeader: header with { RowDataStart = cursor });
    }

    private static PageHeader ReadCommonHeaderCore(HeapPageLayout heapPageLayout) =>
        commonHeaders.ReadPageHeader(source: heapPageLayout.Page.Span);

    private static HeapPageHeader ReadHeaderCore(HeapPageLayout heapPageLayout) =>
        ReadHeapPageHeaderCore(page: heapPageLayout.Page.Span, expectedPageId: heapPageLayout.CommonHeader.PageId.Value);

    private static int ReadFreeBytesCore(HeapPageLayout heapPageLayout)
    {
        var header = ReadHeaderCore(heapPageLayout: heapPageLayout);
        return checked((int)(header.RowDataStart - header.SlotDirectoryEnd));
    }

    private static HeapPageHeader ReadHeapPageHeaderCore(ReadOnlySpan<byte> page, ulong? expectedPageId) =>
        ReadCore(page: page, expectedPageId: expectedPageId)
            .HeapHeader;

    private static HeapSlotEntry ReadHeapSlotEntryCore(ReadOnlySpan<byte> page, ushort slotIndex)
    {
        var header = ReadCore(page: page, expectedPageId: null)
            .HeapHeader;

        return slots.ReadHeapSlotEntry(page: page, slotIndex: slotIndex, slotCount: header.SlotCount, rowDataStart: header.RowDataStart);
    }

    private static int CountLiveRowsCore(HeapPageLayout heapPageLayout)
    {
        var header = ReadHeaderCore(heapPageLayout: heapPageLayout);
        var count = 0;

        for (ushort slotIndex = 0; slotIndex < header.SlotCount; slotIndex++)
        {
            if (ReadHeapSlotEntryCore(page: heapPageLayout.Page.Span, slotIndex: slotIndex)
            .State == HeapSlotState.Live)
            {
                count++;
            }
        }

        return count;
    }
}
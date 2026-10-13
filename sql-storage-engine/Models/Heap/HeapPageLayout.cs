// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Models.Heap;
/// <summary>A validated physical heap-page view, optionally selecting a slot.</summary>
public readonly record struct HeapPageLayout
{
    public PageHeader CommonHeader { get; init; }
    public HeapPageHeader HeapHeader { get; init; }
    public HeapSlotEntry Slot { get; init; }
    public ushort SlotIndex { get; init; }
    public bool HasSlot { get; init; }
    public Memory<byte> Page { get; init; }
    public bool Inserted { get; init; }
    public SlotId ResultSlotId { get; init; }
    public SlotGeneration ResultGeneration { get; init; }
    public HeapReadResult ReadResult { get; init; }
    public ReadOnlyMemory<byte> Row { get; init; }

    private readonly IReadOnlyList<HeapPageRow>? liveRows;
    public IReadOnlyList<HeapPageRow> LiveRows { get => liveRows!; init => liveRows = value; }
}
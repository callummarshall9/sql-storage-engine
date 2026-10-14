// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Heap;
/// <summary>Heap-specific metadata following the common page header.</summary>
public readonly record struct HeapPageHeader
{
    public PageId? PreviousPageId { get; init; }
    public PageId? NextPageId { get; init; }
    public ushort SlotCount { get; init; }
    public uint SlotDirectoryEnd { get; init; }
    public uint RowDataStart { get; init; }
}
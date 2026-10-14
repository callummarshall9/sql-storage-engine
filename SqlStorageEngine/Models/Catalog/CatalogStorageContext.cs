// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogStorageContext
{
    public ulong Id { get; init; }
    public int Operation { get; init; }
    public PageId RootPageId { get; set; }
    public byte[]? Payload { get; set; }
    public IReadOnlyList<PageId>? PageIds { get; set; }
    public TableHeapBinding? HeapBinding { get; set; }
    public IndexTreeBinding? TreeBinding { get; set; }
    public IndexRootContext? RootContext { get; init; }
    public IndexTreeContext? Tree { get; init; }
    public bool IsUnique { get; init; }
    public IndexAllocationContext? Allocations { get; set; }
    public IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)>? Rows { get; set; }
    public IReadOnlyList<LeafIndexEntry>? Entries { get; set; }
    public IAsyncEnumerable<LeafIndexEntry>? EntryStream { get; set; }
}
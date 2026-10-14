// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Models.Heap;
internal sealed record TableHeapContext
{
    public PageId RootPageId { get; set; }
    public HashSet<PageId>? KnownPages { get; init; }
    public RowId RowId { get; init; }
    public PageId PageId { get; init; }
    public ReadOnlyMemory<byte> Row { get; init; }
    public RowId ResultRowId { get; init; }
    public bool Result { get; init; }
    public HeapUpdateResult UpdateResult { get; init; }
    public TableHeapLookupResult LookupResult { get; init; }
    public int ReclaimedBytes { get; init; }
    public int MaximumPages { get; init; }
    public IReadOnlyList<PageId>? PageIds { get; init; }
    public IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)>? Rows { get; init; }
    public decimal PagePercentage { get; init; }
    public long? RepeatableSeed { get; init; }
    public long LiveRowCount { get; init; }
    public int RequiredBytes { get; init; }
    public PageId? ResultPageId { get; init; }
}
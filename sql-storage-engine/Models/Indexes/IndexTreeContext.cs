// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Models.Indexes;
internal sealed record IndexTreeContext
{
    public IndexKey? Key { get; set; }
    public RowId RowId { get; set; }
    public ReadOnlyMemory<byte> IncludedPayload { get; set; }
    public IndexRange? Range { get; set; }
    public PageId RootPageId { get; set; }
    public bool IsUnique { get; set; }
    public IndexInsertResult InsertResult { get; set; }
    public IndexDeleteResult? DeleteResult { get; set; }
    public IReadOnlyList<LeafIndexEntry>? Entries { get; set; }
    public IReadOnlyList<PageId>? PageIds { get; set; }
    public IAsyncEnumerable<LeafIndexEntry>? Rows { get; set; }
    public sql_storage_engine.Models.Catalog.CatalogIndex? Definition { get; set; }
    public int AddCount { get; set; }
    public int RemoveCount { get; set; }
    public IndexTreeContext? Binding { get; set; }
}
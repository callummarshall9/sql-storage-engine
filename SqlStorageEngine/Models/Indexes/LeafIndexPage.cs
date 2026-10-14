// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Indexes;
public sealed record LeafIndexPage
{
    public PageId PageId { get; init; }
    public PageId? ParentPageId { get; init; }
    public PageId? PreviousPageId { get; init; }
    public PageId? NextPageId { get; init; }
    public IReadOnlyList<sql_storage_engine.Models.Indexes.LeafIndexEntry> Entries { get => _entries!; init => _entries = value; }

    private IReadOnlyList<sql_storage_engine.Models.Indexes.LeafIndexEntry>? _entries;
    public IEnumerable<LeafIndexEntry>? CapacityEntries { get; init; }
}
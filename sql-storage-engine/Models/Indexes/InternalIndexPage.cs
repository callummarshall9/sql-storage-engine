// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Indexes;
public sealed record InternalIndexPage
{
    public PageId PageId { get; init; }
    public PageId? ParentPageId { get; init; }
    public IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> Separators { get => _separators!; init => _separators = value; }
    public IReadOnlyList<PageId> Children { get => _children!; init => _children = value; }

    private IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey>? _separators;
    private IReadOnlyList<PageId>? _children;
    public IEnumerable<IndexKey>? CapacitySeparators { get; init; }
}
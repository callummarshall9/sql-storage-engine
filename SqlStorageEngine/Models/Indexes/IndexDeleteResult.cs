// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Indexes;
/// <summary>Describes an exact index deletion and pages that may be reclaimed after it is safe to do so.</summary>
public sealed record IndexDeleteResult
{
    public bool Removed { get; init; }
    public IReadOnlyList<PageId> RetiredPageIds { get => _retiredPageIds!; init => _retiredPageIds = value; }

    private IReadOnlyList<PageId>? _retiredPageIds;
}
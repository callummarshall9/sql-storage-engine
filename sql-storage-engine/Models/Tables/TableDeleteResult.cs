// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Tables;
/// <summary>Describes logical deletion and overflow roots that require deferred reclamation.</summary>
public sealed record TableDeleteResult
{
    public bool Deleted { get; init; }
    public IReadOnlyList<PageId> DeferredCleanupPageIds { get => _deferredCleanupPageIds!; init => _deferredCleanupPageIds = value; }

    private IReadOnlyList<PageId>? _deferredCleanupPageIds;
}
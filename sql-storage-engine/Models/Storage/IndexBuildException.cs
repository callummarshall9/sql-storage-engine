// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Collections.Generic;

namespace sql_storage_engine.Storage;
/// <summary>Reports every index page allocated by a failed build and any page that cleanup could not reclaim.</summary>
public sealed class IndexBuildException : StorageException
{
    public IndexBuildException(string message, IReadOnlyList<sql_storage_engine.Models.Identifiers.PageId> allocatedPageIds, IReadOnlyList<sql_storage_engine.Models.Identifiers.PageId> unreclaimedPageIds, Exception innerException) : base(message, innerException)
    {
        AllocatedPageIds = allocatedPageIds.ToArray();
        UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    }

    public IReadOnlyList<sql_storage_engine.Models.Identifiers.PageId> AllocatedPageIds { get; }
    public IReadOnlyList<sql_storage_engine.Models.Identifiers.PageId> UnreclaimedPageIds { get; }
}
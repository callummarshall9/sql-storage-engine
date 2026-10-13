// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sql_storage_engine.Tables;
/// <summary>Reports a failed logical table mutation and storage roots requiring deferred cleanup.</summary>
public sealed class TableMutationException : StorageException
{
    public TableMutationException(string message, IReadOnlyList<PageId> unreclaimedPageIds, Exception innerException) : base(message, innerException) => UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    public IReadOnlyList<PageId> UnreclaimedPageIds { get; }
}
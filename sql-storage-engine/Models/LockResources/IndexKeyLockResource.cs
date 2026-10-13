// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Models.LockResources;
public sealed record IndexKeyLockResource : LockResource
{
    public IndexId IndexId { get; init; }
    public IndexKey Key { get => key!; init => key = value; }

    private IndexKey? key;
}
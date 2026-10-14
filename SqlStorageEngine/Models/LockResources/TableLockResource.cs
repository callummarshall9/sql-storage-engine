// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.LockResources;
public sealed record TableLockResource : LockResource
{
    public TableId TableId { get; init; }
}
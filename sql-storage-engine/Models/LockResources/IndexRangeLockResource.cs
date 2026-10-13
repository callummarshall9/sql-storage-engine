// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Models.LockResources;
public sealed record IndexRangeLockResource : LockResource
{
    public IndexId IndexId { get; init; }
    public IndexKey? LowerBound { get; init; }
    public IndexKey? UpperBound { get; init; }
    public bool IncludeLowerBound { get; init; }
    public bool IncludeUpperBound { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Overflow;
public readonly record struct OverflowReference
{
    public PageId FirstPageId { get; init; }
    public long TotalLength { get; init; }
}
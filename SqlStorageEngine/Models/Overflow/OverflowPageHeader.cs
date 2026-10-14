// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Overflow;
public readonly record struct OverflowPageHeader
{
    public PageId? NextPageId { get; init; }
    public uint UsedLength { get; init; }
}
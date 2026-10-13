// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Logging;
public sealed record WalSegmentHeader
{
    public DatabaseId DatabaseId { get; init; }
    public ulong Timeline { get; init; }
    public ulong SegmentNumber { get; init; }
}
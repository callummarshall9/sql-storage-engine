// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Strategies;
internal sealed record StorageObjectManifest
{
    public Guid DatabaseId { get; init; }
    public long Commit { get; init; }
    public int PageSize { get; init; }
    public int SegmentSize { get; init; }
    public long FileLength { get; init; }
    public StorageObjectSegment[]? Segments { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Strategies;
public sealed record StorageStrategyOptions
{
    public string? Directory { get; init; }
    public string? ObjectPrefix { get; init; }
    public int SegmentSize { get; init; }
    public int UploadConcurrency { get; init; }
    public long MaximumCacheBytes { get; init; }
}
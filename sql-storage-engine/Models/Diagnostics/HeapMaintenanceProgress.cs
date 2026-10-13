// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Diagnostics;
public sealed record HeapMaintenanceProgress
{
    public int NextPageIndex { get; init; }
    public long ReclaimedBytes { get; init; }
    public long FailureCount { get; init; }
    public bool Complete { get; init; }
}
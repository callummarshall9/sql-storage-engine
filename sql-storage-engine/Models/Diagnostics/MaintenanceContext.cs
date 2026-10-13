// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record MaintenanceContext
{
    public object? CheckpointSource { get; init; }
    public Func<CancellationToken, ValueTask<HeapMaintenanceProgress?>>? Read { get; init; }
    public Func<HeapMaintenanceProgress, CancellationToken, ValueTask>? Write { get; init; }
    public Func<CancellationToken, ValueTask>? Throttle { get; init; }
    public TableId TableId { get; init; }
    public HeapMaintenanceProgress? Progress { get; set; }
    public IReadOnlyList<PageId>? Pages { get; set; }
    public int MaximumPages { get; set; }
    public int Index { get; set; }
    public int End { get; set; }
    public int ReclaimedBytes { get; set; }
}
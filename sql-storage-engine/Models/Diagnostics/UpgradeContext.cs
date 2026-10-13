// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record UpgradeContext
{
    public string? DatabasePath { get; init; }
    public string? BackupDirectory { get; init; }
    public ushort TargetVersion { get; init; }
    public ushort SourceVersion { get; init; }
    public int PageSize { get; init; }
    public byte[]? Bytes { get; init; }
    public UpgradeProgress? Progress { get; init; }
    public List<string>? Activity { get; init; }
    public Func<int, CancellationToken, ValueTask>? Boundary { get; init; }
    public Func<string, CancellationToken, Task<bool>>? CheckIntegrity { get; init; }
    public bool CreatedProgress { get; init; }
    public bool VerifiedBackup { get; init; }
}
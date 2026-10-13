// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Diagnostics;
public sealed record UpgradeProgress
{
    public ushort SourceVersion { get; init; }
    public ushort TargetVersion { get; init; }
    public int NextStep { get; init; }
    public UpgradeStatus Status { get; init; }
}
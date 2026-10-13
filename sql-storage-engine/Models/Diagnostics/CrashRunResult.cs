// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record CrashRunResult
{
    public int Ordinal { get; init; }
    public CrashBoundary Boundary { get; init; }
    public bool ExpectedCommitted { get; init; }
}
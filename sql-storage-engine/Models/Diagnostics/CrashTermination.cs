// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Models.Diagnostics;
/// <summary>Identifies the durable-write boundary and ordinal that terminated a simulated run.</summary>
public readonly record struct CrashTermination
{
    public CrashBoundary Boundary { get; init; }
    public int Ordinal { get; init; }
}
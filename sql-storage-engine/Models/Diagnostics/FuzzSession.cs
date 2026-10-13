// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record FuzzSession
{
    public FuzzTarget? Target { get; init; }
    public int Iterations { get; init; }
    public int Seed { get; init; }
    public TimeSpan Timeout { get; init; }
    public Action<string, byte[], string, string>? Save { get; init; }
    public IReadOnlyList<FuzzFailure>? Failures { get; init; }
}
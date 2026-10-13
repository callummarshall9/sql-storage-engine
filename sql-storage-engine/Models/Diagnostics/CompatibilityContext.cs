// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record CompatibilityContext
{
    public string? FixturePath { get; init; }
    public string? DatabasePath { get; init; }
    public CompatibilityFixture? Fixture { get; init; }
    public byte[]? Bytes { get; init; }
    public DatabaseHeader? Header { get; init; }
    public IReadOnlyList<WalRecord>? Records { get; init; }
    public CompatibilityResult? Result { get; init; }
}
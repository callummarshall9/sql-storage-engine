// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record IntegrityReport
{
    public IReadOnlyList<IntegrityFinding> Findings { get => findings!; init => findings = value; }

    private IReadOnlyList<IntegrityFinding>? findings;
    public bool IsHealthy { get; init; }
}
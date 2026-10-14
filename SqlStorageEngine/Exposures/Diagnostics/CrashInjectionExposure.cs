// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.CrashInjections;

namespace sql_storage_engine.Exposures.Diagnostics;
/// <summary>Records or terminates execution at deterministic durable-write boundaries.</summary>
public sealed class CrashInjectionExposure
{
    private static readonly ICrashInjectionService service = new CrashInjectionService();
    private readonly CrashInjectionState state = new()
    {
        Boundaries = []
    };
    public CrashInjectionExposure(int? terminateAtOrdinal = null)
    {
        state.TerminateAtOrdinal = terminateAtOrdinal;
    }

    public IReadOnlyList<CrashBoundary> GetBoundaries() =>
        service.GetCrashInjectionStateBoundaries(crashInjectionState: state);

    public void Reach(CrashBoundary boundary) =>
        service.ReachCrashInjectionState(crashInjectionState: state, boundary: boundary);
}
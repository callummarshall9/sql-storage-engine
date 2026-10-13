// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.CrashInjections;
/// <summary>Records deterministic crash points and raises the selected simulated termination.</summary>
internal sealed partial class CrashInjectionService : ICrashInjectionService
{
    public void ReachCrashInjectionState(CrashInjectionState crashInjectionState, CrashBoundary boundary) =>
        TryCatch(operation: () =>
    {
        ValidateCrashInjectionStateOnReach(crashInjectionState: crashInjectionState, boundary: boundary);
        crashInjectionState.Boundaries!.Add(item: boundary);
        var ordinal = crashInjectionState.Ordinal++;

        if (crashInjectionState.TerminateAtOrdinal == ordinal)
        {
            throw new SimulatedProcessTerminationException(crashTermination: new CrashTermination { Boundary = boundary, Ordinal = ordinal });
        }
    });

    public IReadOnlyList<CrashBoundary> GetCrashInjectionStateBoundaries(CrashInjectionState crashInjectionState) =>
        TryCatch(operation: () =>
    {
        ValidateCrashInjectionStateBoundariesOnGet(crashInjectionState: crashInjectionState);
        return Array.AsReadOnly(array: crashInjectionState.Boundaries!.ToArray());
    });
}
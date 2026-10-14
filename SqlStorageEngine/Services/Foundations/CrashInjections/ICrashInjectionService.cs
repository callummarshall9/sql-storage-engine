// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.CrashInjections;
internal interface ICrashInjectionService
{
    void ReachCrashInjectionState(CrashInjectionState crashInjectionState, CrashBoundary boundary);

    IReadOnlyList<CrashBoundary> GetCrashInjectionStateBoundaries(CrashInjectionState crashInjectionState);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed class CrashInjectionState
{
    public int Ordinal;
    public int? TerminateAtOrdinal;
    public List<CrashBoundary>? Boundaries;
}
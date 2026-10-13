// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.CrashInjections;
using sql_storage_engine.Services.Processings.CrashRuns;

namespace sql_storage_engine.Exposures.Diagnostics;
public static class CrashBoundaryHarnessExposure
{
    private static readonly ICrashInjectionHarnessProcessingService service = new CrashInjectionHarnessProcessingService(crashInjections: new CrashInjectionService());
    public static Task<IReadOnlyList<CrashRunResult>> RunEveryBoundaryAsync<TState>(Func<Action<CrashBoundary>, Task> execute, Func<Task<TState>> reopenAndRecover, Func<int, bool> expectedCommitted, Func<TState, bool, Task> verifyState, Func<TState, Task> verifyIntegrity) =>
        service.RunAllCrashRunResultsAsync(execute: execute, reopenAndRecover: reopenAndRecover, expectedCommitted: expectedCommitted, verifyState: verifyState, verifyIntegrity: verifyIntegrity);
}
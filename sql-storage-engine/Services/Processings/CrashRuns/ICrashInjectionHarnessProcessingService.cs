// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.CrashRuns;
internal interface ICrashInjectionHarnessProcessingService
{
    Task<IReadOnlyList<CrashRunResult>> RunAllCrashRunResultsAsync<TState>(Func<Action<CrashBoundary>, Task> execute, Func<Task<TState>> reopenAndRecover, Func<int, bool> expectedCommitted, Func<TState, bool, Task> verifyState, Func<TState, Task> verifyIntegrity);
}
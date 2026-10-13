// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Services.Processings.CrashRuns;
internal sealed partial class CrashInjectionHarnessProcessingService
{
    private static void ValidateCallbacks<TState>(Func<Action<CrashBoundary>, Task> execute, Func<Task<TState>> reopenAndRecover, Func<int, bool> expectedCommitted, Func<TState, bool, Task> verifyState, Func<TState, Task> verifyIntegrity) =>
        Validate(inputs: [(execute is null, nameof(execute)), (reopenAndRecover is null, nameof(reopenAndRecover)), (expectedCommitted is null, nameof(expectedCommitted)), (verifyState is null, nameof(verifyState)), (verifyIntegrity is null, nameof(verifyIntegrity))]);

    private static void Validate(ReadOnlySpan<(bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
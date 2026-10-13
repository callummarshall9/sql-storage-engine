// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.CrashInjections;

namespace sql_storage_engine.Services.Processings.CrashRuns;
/// <summary>Discovers crash points and verifies recovered state after each selected termination.</summary>
internal sealed partial class CrashInjectionHarnessProcessingService(ICrashInjectionService crashInjections) : ICrashInjectionHarnessProcessingService
{
    public Task<IReadOnlyList<CrashRunResult>> RunAllCrashRunResultsAsync<TState>(Func<Action<CrashBoundary>, Task> execute, Func<Task<TState>> reopenAndRecover, Func<int, bool> expectedCommitted, Func<TState, bool, Task> verifyState, Func<TState, Task> verifyIntegrity) =>
        TryCatch<IReadOnlyList<CrashRunResult>>(operation: async () =>
    {
        ValidateCallbacks(execute: execute, reopenAndRecover: reopenAndRecover, expectedCommitted: expectedCommitted, verifyState: verifyState, verifyIntegrity: verifyIntegrity);

        var discovery = new CrashInjectionState
        {
            Boundaries = []
        };

        await execute(arg: boundary => crashInjections.ReachCrashInjectionState(crashInjectionState: discovery, boundary: boundary))
            .ConfigureAwait(continueOnCapturedContext: false);

        var boundaries = crashInjections.GetCrashInjectionStateBoundaries(crashInjectionState: discovery);
        var results = new List<CrashRunResult>();

        for (var ordinal = 0; ordinal < boundaries.Count; ordinal++)
        {
            var injection = new CrashInjectionState
            {
                Boundaries = [],
                TerminateAtOrdinal = ordinal
            };

            try
            {
                await execute(arg: boundary => crashInjections.ReachCrashInjectionState(crashInjectionState: injection, boundary: boundary))
            .ConfigureAwait(continueOnCapturedContext: false);

                throw new InvalidOperationException(message: "Boundary did not terminate execution.");
            }
            catch (SimulatedProcessTerminationException exception)when (exception.Ordinal == ordinal)
            {
            }

            var state = await reopenAndRecover()
            .ConfigureAwait(continueOnCapturedContext: false);

            var committed = expectedCommitted(arg: ordinal);

            await verifyState(arg1: state, arg2: committed)
            .ConfigureAwait(continueOnCapturedContext: false);

            await verifyIntegrity(arg: state)
            .ConfigureAwait(continueOnCapturedContext: false);

            results.Add(item: new CrashRunResult { Ordinal = ordinal, Boundary = boundaries[ordinal], ExpectedCommitted = committed });
        }

        return results.AsReadOnly();
    });
}
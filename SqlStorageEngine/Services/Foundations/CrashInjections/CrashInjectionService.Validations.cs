// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.CrashInjections;
internal sealed partial class CrashInjectionService
{
    private static void ValidateCrashInjectionStateOnReach(CrashInjectionState crashInjectionState, CrashBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(argument: crashInjectionState);
        Validate(inputs: [(crashInjectionState.Boundaries is null, true, nameof(crashInjectionState.Boundaries)), (!Enum.IsDefined(value: boundary), false, nameof(boundary)), (crashInjectionState.TerminateAtOrdinal < 0, false, nameof(crashInjectionState.TerminateAtOrdinal)), (crashInjectionState.Ordinal == int.MaxValue, false, nameof(crashInjectionState.Ordinal))]);
    }

    private static void ValidateCrashInjectionStateBoundariesOnGet(CrashInjectionState crashInjectionState)
    {
        ArgumentNullException.ThrowIfNull(argument: crashInjectionState);
        Validate(inputs: [(crashInjectionState.Boundaries is null, true, nameof(crashInjectionState.Boundaries))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
        }
    }
}
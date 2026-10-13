// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.FuzzSessions;
internal sealed partial class FuzzSessionService
{
    private static void ValidateFuzzSession(FuzzSession fuzzSession, CancellationToken cancellationToken)
    {
        Validate(inputs: [(fuzzSession is null, nameof(fuzzSession)), (fuzzSession?.Target is null, nameof(FuzzSession.Target)), (fuzzSession?.Save is null, nameof(FuzzSession.Save)), (fuzzSession?.Iterations <= 0, nameof(FuzzSession.Iterations)), (fuzzSession?.Timeout <= TimeSpan.Zero, nameof(FuzzSession.Timeout)), (fuzzSession?.Target?.Decode is null, nameof(FuzzTarget.Decode)), (string.IsNullOrWhiteSpace(value: fuzzSession?.Target?.Name), nameof(FuzzTarget.Name)), (fuzzSession?.Target?.GoldenSeeds is null || fuzzSession.Target.GoldenSeeds.Count == 0, nameof(FuzzTarget.GoldenSeeds)), (fuzzSession?.Target?.MaximumInputBytes <= 0 || fuzzSession?.Target?.MaximumInputBytes == int.MaxValue, nameof(FuzzTarget.MaximumInputBytes))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Valid bounded fuzz inputs are required.", paramName: input.ParameterName);
            }
        }
    }
}
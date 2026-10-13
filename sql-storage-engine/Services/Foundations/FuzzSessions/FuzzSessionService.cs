// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Brokers.Fuzzing;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.FuzzSessions;
internal sealed partial class FuzzSessionService : IFuzzSessionService
{
    private readonly FuzzExecutionBroker execution = new FuzzExecutionBroker();
    public ValueTask<FuzzSession> RunFuzzSessionAsync(FuzzSession fuzzSession, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateFuzzSession(fuzzSession: fuzzSession, cancellationToken: cancellationToken);
        var target = fuzzSession.Target!;
        using var run = execution.Open(seed: fuzzSession.Seed, save: fuzzSession.Save!);
        var failures = new List<FuzzFailure>();

        for (var iteration = 0; iteration < fuzzSession.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = target.GoldenSeeds[iteration % target.GoldenSeeds.Count];
            var limit = Math.Min(val1: target.MaximumInputBytes, val2: Math.Max(val1: 1, val2: source.Length * 2));
            var length = execution.Next(run: run, maximum: limit + 1);
            var input = new byte[length];

            source.Span.Slice(start: 0, length: Math.Min(val1: source.Length, val2: input.Length))
            .CopyTo(destination: input);

            for (var mutation = 0; mutation < Math.Min(val1: 8, val2: input.Length); mutation++)
            {
                input[execution.Next(run: run, maximum: input.Length)] = (byte)execution.Next(run: run, maximum: 256);
            }

            var outcome = await execution.ExecuteAsync(run: run, decode: target.Decode, bytes: input, timeout: fuzzSession.Timeout, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            FuzzFailure? failure = null;

            if (outcome.TimedOut)
            {
                failure = new FuzzFailure
                {
                    Target = target.Name,
                    Input = input,
                    FailureType = "TIMEOUT",
                    Message = "Decoder exceeded its execution bound."
                };
            }
            else if (outcome.Failure is not null && outcome.Failure is not (ArgumentException or StorageException or JsonException or OverflowException))
            {
                failure = new FuzzFailure
                {
                    Target = target.Name,
                    Input = input,
                    FailureType = outcome.Failure.GetType()
            .Name,
                    Message = outcome.Failure.Message
                };
            }

            if (failure is not null)
            {
                execution.Save(run: run, target: failure.Target, bytes: failure.Input, type: failure.FailureType, message: failure.Message);
                failures.Add(item: failure);
            }
        }

        return fuzzSession with
        {
            Failures = failures.AsReadOnly()
        };
    });
}
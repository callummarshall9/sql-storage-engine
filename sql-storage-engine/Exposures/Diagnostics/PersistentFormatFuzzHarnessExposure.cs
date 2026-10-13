// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.FuzzSessions;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class PersistentFormatFuzzHarnessExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private readonly IFuzzSessionService service = new FuzzSessionService();
    private readonly FuzzSession session;
    public PersistentFormatFuzzHarnessExposure(IFuzzRegressionSinkExposure regressionSink, TimeSpan? timeout = null)
    {
        session = new FuzzSession
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(seconds: 1),
            Save = (target, input, type, message) => regressionSink.Save(fuzzFailure: new FuzzFailure { Target = target, Input = input, FailureType = type, Message = message })
        };
    }

    public async Task<IReadOnlyList<FuzzFailure>> RunAsync(FuzzTarget fuzzTarget, int iterations, int seed, CancellationToken cancellationToken = default) =>
        (await service.RunFuzzSessionAsync(fuzzSession: session with { Target = fuzzTarget, Iterations = iterations, Seed = seed }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Failures!;
}
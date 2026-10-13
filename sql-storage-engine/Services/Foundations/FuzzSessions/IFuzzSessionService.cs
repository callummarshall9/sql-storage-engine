// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.FuzzSessions;
internal interface IFuzzSessionService
{
    ValueTask<FuzzSession> RunFuzzSessionAsync(FuzzSession fuzzSession, CancellationToken cancellationToken);
}
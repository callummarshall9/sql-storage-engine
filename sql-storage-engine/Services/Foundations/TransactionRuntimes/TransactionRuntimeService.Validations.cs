// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.TransactionRuntimes;
internal sealed partial class TransactionRuntimeService
{
    private static void ValidateRequest(EngineResourceContext engineResourceContext)
    {
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext);
    }

    private static void ValidateRequest(EngineResourceContext engineResourceContext, CancellationToken cancellationToken)
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
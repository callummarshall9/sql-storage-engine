// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Orchestrations.ScopedStreams;
internal sealed partial class ScopedStreamOrchestrationService<T>
{
    private static void ValidateSource(EngineResourceContext engineResourceContext)
    {
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.StreamSource);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.Access);

        if (engineResourceContext.StreamSource is not IAsyncEnumerable<T>)
        {
            throw new ArgumentException(message: "Invalid stream element type.");
        }
    }
}
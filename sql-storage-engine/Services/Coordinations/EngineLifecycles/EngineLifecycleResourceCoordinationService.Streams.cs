// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineLifecycles;
internal sealed partial class EngineLifecycleResourceCoordinationService
{
    public EngineResourceContext TrackEngineResourceContextStream<T>(EngineResourceContext engineResourceContext) =>
        TryCatch(operation: () =>
    {
        ValidateStreamSource<T>(engineResourceContext: engineResourceContext);

        if (engineResourceContext.Access is null)
        {
            return engineResourceContext;
        }

        return sql_storage_engine.Services.Orchestrations.ScopedStreams.ScopedStreamOrchestrationService<T>.BindScopedStreamOrchestrationService(engineAccessBinding: access.ReadEngineAccessBinding())
            .TrackEngineResourceContextStream(engineResourceContext: engineResourceContext);
    });

    private static void ValidateStreamSource<T>(EngineResourceContext engineResourceContext)
    {
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.StreamSource);

        if (engineResourceContext.StreamSource is not IAsyncEnumerable<T>)
        {
            throw new ArgumentException(message: "The stream source is incompatible with its element type.", paramName: nameof(engineResourceContext));
        }
    }
}
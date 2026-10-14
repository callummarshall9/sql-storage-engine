// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal sealed partial class EngineResourceContextManagementService
{
    public EngineResourceContext TrackEngineResourceContextStream<T>(EngineResourceContext engineResourceContext) =>
        TryCatch(operation: () =>
    {
        ValidateStreamSource<T>(engineResourceContext: engineResourceContext);
        return lifecycle.TrackEngineResourceContextStream<T>(engineResourceContext: engineResourceContext);
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
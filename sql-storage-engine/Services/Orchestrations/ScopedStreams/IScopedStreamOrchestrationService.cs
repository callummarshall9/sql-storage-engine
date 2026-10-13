// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Orchestrations.ScopedStreams;
internal interface IScopedStreamOrchestrationService<T>
{
    EngineResourceContext TrackEngineResourceContextStream(EngineResourceContext engineResourceContext);
}
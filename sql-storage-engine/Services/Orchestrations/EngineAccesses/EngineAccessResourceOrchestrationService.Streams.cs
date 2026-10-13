// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Orchestrations.EngineAccesses;
internal sealed partial class EngineAccessResourceOrchestrationService
{
    internal EngineAccessBinding ReadEngineAccessBinding() =>
        new EngineAccessBinding
    {
        Service = access
    };
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityWal;
internal interface ICompatibilityWalOrchestrationService
{
    CompatibilityContext ReadCompatibilityContext(CompatibilityContext compatibilityContext);
}
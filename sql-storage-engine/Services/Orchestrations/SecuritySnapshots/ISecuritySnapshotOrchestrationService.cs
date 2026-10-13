// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecuritySnapshots;
internal interface ISecuritySnapshotOrchestrationService
{
    SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext);
}
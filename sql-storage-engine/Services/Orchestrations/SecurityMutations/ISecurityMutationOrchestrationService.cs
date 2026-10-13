// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityMutations;
internal interface ISecurityMutationOrchestrationService
{
    SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext);
}
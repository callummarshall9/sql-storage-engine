// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies;
internal interface ISecurityAdmissionPolicyOrchestrationService
{
    SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext);
}
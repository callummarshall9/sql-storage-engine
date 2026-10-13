// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies;
internal sealed partial class SecurityAdmissionPolicyOrchestrationService
{
    private static void ValidateSecuritySessionContext(SecuritySessionContext securitySessionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: securitySessionContext);
        ArgumentNullException.ThrowIfNull(argument: securitySessionContext.Admission);
        ArgumentNullException.ThrowIfNull(argument: securitySessionContext.Admission.Request);
    }
}
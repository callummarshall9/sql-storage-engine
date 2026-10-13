// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecuritySnapshots;
internal sealed partial class SecuritySnapshotOrchestrationService
{
    private static void ValidateSecuritySessionContext(SecuritySessionContext securitySessionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: securitySessionContext);
    }
}
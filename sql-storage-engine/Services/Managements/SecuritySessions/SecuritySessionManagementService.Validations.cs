// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Managements.SecuritySessions;
internal sealed partial class SecuritySessionManagementService
{
    private static void ValidateSecuritySessionContext(SecuritySessionContext securitySessionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: securitySessionContext);
    }

    private static void ValidateSecuritySessionContext(SecuritySessionContext securitySessionContext, CancellationToken cancellationToken)
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Managements.SecuritySessions;
internal interface ISecuritySessionManagementService
{
    ValueTask<SecuritySessionContext> ProcessSecuritySessionContextAsync(SecuritySessionContext securitySessionContext, CancellationToken cancellationToken);

    SecuritySessionContext ReadSecuritySessionContextSnapshot(SecuritySessionContext securitySessionContext);
}
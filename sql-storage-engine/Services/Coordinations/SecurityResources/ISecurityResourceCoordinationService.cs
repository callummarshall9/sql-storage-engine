// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Coordinations.SecurityResources;
internal interface ISecurityResourceCoordinationService
{
    ValueTask<SecuritySessionContext> ProcessSecuritySessionContextAsync(SecuritySessionContext securitySessionContext, CancellationToken cancellationToken);

    SecuritySessionContext ReadSecuritySessionContextSnapshot(SecuritySessionContext securitySessionContext);
}
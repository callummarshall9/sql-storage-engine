// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityEvents;
internal interface ISecurityEventContextOrchestrationService
{
    ValueTask AppendSecurityEventContextAsync(SecurityEventContext securityEventContext, CancellationToken token);

    ValueTask<SecurityEventContext> ReadSecurityEventContextAsync(SecurityEventContext securityEventContext, CancellationToken token);
}
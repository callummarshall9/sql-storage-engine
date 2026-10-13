// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Coordinations.SecurityResources;
internal sealed partial class SecurityResourceCoordinationService : ISecurityResourceCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.SecurityEvents.SecurityEventContextOrchestrationService events;
    private readonly sql_storage_engine.Services.Orchestrations.SecuritySnapshots.SecuritySnapshotOrchestrationService snapshots;
    private SecurityResourceCoordinationService(sql_storage_engine.Services.Orchestrations.SecurityEvents.SecurityEventContextOrchestrationService events, sql_storage_engine.Services.Orchestrations.SecuritySnapshots.SecuritySnapshotOrchestrationService snapshots)
    {
        this.events = events;
        this.snapshots = snapshots;
    }

    internal static SecurityResourceCoordinationService CreateSecurityResourceCoordinationService() =>
        new(events: sql_storage_engine.Services.Orchestrations.SecurityEvents.SecurityEventContextOrchestrationService.CreateSecurityEventContextOrchestrationService(), snapshots: sql_storage_engine.Services.Orchestrations.SecuritySnapshots.SecuritySnapshotOrchestrationService.CreateSecuritySnapshotOrchestrationService());

    public ValueTask<SecuritySessionContext> ProcessSecuritySessionContextAsync(SecuritySessionContext securitySessionContext, CancellationToken cancellationToken) =>
        TryCatch<SecuritySessionContext>(operation: async () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext, cancellationToken: cancellationToken);

        if (securitySessionContext.Operation == 0)
        {
            return snapshots.ProcessSecuritySessionContext(securitySessionContext: securitySessionContext);
        }

        if (securitySessionContext.Operation == 8)
        {
            await events.AppendSecurityEventContextAsync(securityEventContext: securitySessionContext.Events!, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return securitySessionContext;
        }

        return securitySessionContext with
        {
            Events = await events.ReadSecurityEventContextAsync(securityEventContext: securitySessionContext.Events!, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public SecuritySessionContext ReadSecuritySessionContextSnapshot(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        return snapshots.ProcessSecuritySessionContext(securitySessionContext: securitySessionContext);
    });
}
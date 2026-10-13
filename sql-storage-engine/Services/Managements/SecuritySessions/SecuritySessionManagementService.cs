// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Managements.SecuritySessions;
internal sealed partial class SecuritySessionManagementService : ISecuritySessionManagementService
{
    private readonly sql_storage_engine.Services.Coordinations.SecurityCatalogs.SecurityCatalogCoordinationService catalogs;
    private readonly sql_storage_engine.Services.Coordinations.SecurityResources.SecurityResourceCoordinationService resources;
    private SecuritySessionManagementService(sql_storage_engine.Services.Coordinations.SecurityCatalogs.SecurityCatalogCoordinationService catalogs, sql_storage_engine.Services.Coordinations.SecurityResources.SecurityResourceCoordinationService resources)
    {
        this.catalogs = catalogs;
        this.resources = resources;
    }

    internal static SecuritySessionManagementService CreateSecuritySessionManagementService() =>
        new(catalogs: sql_storage_engine.Services.Coordinations.SecurityCatalogs.SecurityCatalogCoordinationService.CreateSecurityCatalogCoordinationService(), resources: sql_storage_engine.Services.Coordinations.SecurityResources.SecurityResourceCoordinationService.CreateSecurityResourceCoordinationService());

    public ValueTask<SecuritySessionContext> ProcessSecuritySessionContextAsync(SecuritySessionContext securitySessionContext, CancellationToken cancellationToken) =>
        TryCatch<SecuritySessionContext>(operation: async () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext, cancellationToken: cancellationToken);

        return securitySessionContext.Operation is 0 or 8 or 9 ? await resources.ProcessSecuritySessionContextAsync(securitySessionContext: securitySessionContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : catalogs.ProcessSecuritySessionContext(securitySessionContext: securitySessionContext);
    });

    public SecuritySessionContext ReadSecuritySessionContextSnapshot(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        return resources.ReadSecuritySessionContextSnapshot(securitySessionContext: securitySessionContext);
    });
}
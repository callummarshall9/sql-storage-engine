// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Coordinations.SecurityCatalogs;
internal sealed partial class SecurityCatalogCoordinationService : ISecurityCatalogCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.SecurityMutations.SecurityMutationOrchestrationService mutations;
    private readonly sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies.SecurityAdmissionPolicyOrchestrationService admissions;
    private SecurityCatalogCoordinationService(sql_storage_engine.Services.Orchestrations.SecurityMutations.SecurityMutationOrchestrationService mutations, sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies.SecurityAdmissionPolicyOrchestrationService admissions)
    {
        this.mutations = mutations;
        this.admissions = admissions;
    }

    internal static SecurityCatalogCoordinationService CreateSecurityCatalogCoordinationService() =>
        new(mutations: sql_storage_engine.Services.Orchestrations.SecurityMutations.SecurityMutationOrchestrationService.CreateSecurityMutationOrchestrationService(), admissions: sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies.SecurityAdmissionPolicyOrchestrationService.CreateSecurityAdmissionPolicyOrchestrationService());

    public SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        return securitySessionContext.Operation == 6 ? admissions.ProcessSecuritySessionContext(securitySessionContext: securitySessionContext) : mutations.ProcessSecuritySessionContext(securitySessionContext: securitySessionContext);
    });
}
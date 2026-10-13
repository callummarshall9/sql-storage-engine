// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecurityAdmissionPolicies;
internal sealed partial class SecurityAdmissionPolicyOrchestrationService : ISecurityAdmissionPolicyOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.SecurityAdmissions.SecurityAdmissionService admissions;
    private readonly sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService principals;
    private SecurityAdmissionPolicyOrchestrationService(sql_storage_engine.Services.Foundations.SecurityAdmissions.SecurityAdmissionService admissions, sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService principals)
    {
        this.admissions = admissions;
        this.principals = principals;
    }

    internal static SecurityAdmissionPolicyOrchestrationService CreateSecurityAdmissionPolicyOrchestrationService() =>
        new(admissions: new sql_storage_engine.Services.Foundations.SecurityAdmissions.SecurityAdmissionService(), principals: new sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService());

    public SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        principals.ValidateStoragePrincipalId(storagePrincipalId: securitySessionContext.Admission!.Request!.Principal);

        return securitySessionContext with
        {
            Admission = admissions.PrepareSecurityAdmission(securityAdmission: securitySessionContext.Admission)
        };
    });
}
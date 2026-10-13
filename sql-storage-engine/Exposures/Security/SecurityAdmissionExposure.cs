// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecurityAdmissions;

namespace sql_storage_engine.Exposures.Security;
internal static class SecurityAdmissionExposure
{
    private static readonly ISecurityAdmissionService service = new SecurityAdmissionService();
    internal static SecurityAdmission PrepareSecurityAdmission(SecurityAdmission securityAdmission) =>
        service.PrepareSecurityAdmission(securityAdmission: securityAdmission);
}
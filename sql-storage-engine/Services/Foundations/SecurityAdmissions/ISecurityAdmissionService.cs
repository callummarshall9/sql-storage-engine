// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityAdmissions;
internal interface ISecurityAdmissionService
{
    SecurityAdmission PrepareSecurityAdmission(SecurityAdmission securityAdmission);
}
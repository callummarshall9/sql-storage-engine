// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityAdmissions;
internal sealed partial class SecurityAdmissionService
{
    private static void ValidateSecurityAdmission(SecurityAdmission securityAdmission) =>
        Validate(inputs: [(securityAdmission is null, nameof(securityAdmission)), (securityAdmission?.State is null, nameof(SecurityAdmission.State)), (securityAdmission?.Tables is null, nameof(SecurityAdmission.Tables)), (securityAdmission?.Request is null, nameof(SecurityAdmission.Request)), (securityAdmission?.Request?.AccessSource is null && securityAdmission?.Request?.Access is null, nameof(StorageAuthorizationRequest.Access))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
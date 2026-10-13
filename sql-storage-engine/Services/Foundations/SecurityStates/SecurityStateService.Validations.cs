// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityStates;
internal sealed partial class SecurityStateService
{
    private static void ValidateSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        Validate(inputs: [(securityStatePayload is null, nameof(securityStatePayload))]);

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
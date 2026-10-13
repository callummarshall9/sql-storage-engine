// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityCatalogMutations;
internal sealed partial class SecurityCatalogMutationService
{
    private static void ValidateSecurityCatalogMutation(SecurityCatalogMutation securityCatalogMutation) =>
        Validate(inputs: [(securityCatalogMutation is null, nameof(securityCatalogMutation)), (securityCatalogMutation?.State is null, nameof(SecurityCatalogMutation.State)), (securityCatalogMutation?.Tables is null, nameof(SecurityCatalogMutation.Tables))]);

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
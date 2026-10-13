// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityPrincipals;
internal sealed partial class SecurityPrincipalService
{
    private static void ValidateStoragePrincipalIdInput(StoragePrincipalId storagePrincipalId) =>
        Validate(inputs: [(storagePrincipalId.Issuer == Guid.Empty || storagePrincipalId.Subject == Guid.Empty, nameof(storagePrincipalId))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A principal needs nonempty issuer and subject identities.", paramName: input.ParameterName);
            }
        }
    }
}
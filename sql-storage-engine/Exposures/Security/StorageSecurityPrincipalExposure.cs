// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecurityPrincipals;

namespace sql_storage_engine.Exposures.Security;
internal static class StorageSecurityPrincipalExposure
{
    private static readonly ISecurityPrincipalService service = new SecurityPrincipalService();
    internal static void ValidatePrincipal(StoragePrincipalId principal) =>
        service.ValidateStoragePrincipalId(storagePrincipalId: principal);
}
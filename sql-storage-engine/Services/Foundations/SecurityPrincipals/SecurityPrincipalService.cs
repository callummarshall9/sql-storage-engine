// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityPrincipals;
internal sealed partial class SecurityPrincipalService : ISecurityPrincipalService
{
    public void ValidateStoragePrincipalId(StoragePrincipalId storagePrincipalId) =>
        TryCatch(operation: () =>
    {
        ValidateStoragePrincipalIdInput(storagePrincipalId: storagePrincipalId);
    });
}
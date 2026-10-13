// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityPrincipals;
internal interface ISecurityPrincipalService
{
    void ValidateStoragePrincipalId(StoragePrincipalId storagePrincipalId);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityCatalogMutations;
internal interface ISecurityCatalogMutationService
{
    SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipal(SecurityCatalogMutation securityCatalogMutation);

    SecurityCatalogMutation PrepareSecurityCatalogMutationPermission(SecurityCatalogMutation securityCatalogMutation);

    SecurityCatalogMutation PrepareSecurityCatalogMutationDatabasePermission(SecurityCatalogMutation securityCatalogMutation);

    SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalDependency(SecurityCatalogMutation securityCatalogMutation);

    SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalChange(SecurityCatalogMutation securityCatalogMutation);
}
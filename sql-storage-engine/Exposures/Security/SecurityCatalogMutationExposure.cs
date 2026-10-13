// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecurityCatalogMutations;

namespace sql_storage_engine.Exposures.Security;
internal static class SecurityCatalogMutationExposure
{
    private static readonly ISecurityCatalogMutationService service = new SecurityCatalogMutationService();
    internal static SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipal(SecurityCatalogMutation securityCatalogMutation) =>
        service.PrepareSecurityCatalogMutationPrincipal(securityCatalogMutation: securityCatalogMutation);

    internal static SecurityCatalogMutation PrepareSecurityCatalogMutationPermission(SecurityCatalogMutation securityCatalogMutation) =>
        service.PrepareSecurityCatalogMutationPermission(securityCatalogMutation: securityCatalogMutation);

    internal static SecurityCatalogMutation PrepareSecurityCatalogMutationDatabasePermission(SecurityCatalogMutation securityCatalogMutation) =>
        service.PrepareSecurityCatalogMutationDatabasePermission(securityCatalogMutation: securityCatalogMutation);

    internal static SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalDependency(SecurityCatalogMutation securityCatalogMutation) =>
        service.PrepareSecurityCatalogMutationPrincipalDependency(securityCatalogMutation: securityCatalogMutation);

    internal static SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalChange(SecurityCatalogMutation securityCatalogMutation) =>
        service.PrepareSecurityCatalogMutationPrincipalChange(securityCatalogMutation: securityCatalogMutation);
}
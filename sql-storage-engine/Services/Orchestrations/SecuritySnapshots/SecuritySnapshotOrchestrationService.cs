// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Orchestrations.SecuritySnapshots;
internal sealed partial class SecuritySnapshotOrchestrationService : ISecuritySnapshotOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.SecuritySnapshots.StorageSecuritySnapshotService snapshots;
    private readonly sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService principals;
    private SecuritySnapshotOrchestrationService(sql_storage_engine.Services.Foundations.SecuritySnapshots.StorageSecuritySnapshotService snapshots, sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService principals)
    {
        this.snapshots = snapshots;
        this.principals = principals;
    }

    internal static SecuritySnapshotOrchestrationService CreateSecuritySnapshotOrchestrationService() =>
        new(snapshots: new sql_storage_engine.Services.Foundations.SecuritySnapshots.StorageSecuritySnapshotService(), principals: new sql_storage_engine.Services.Foundations.SecurityPrincipals.SecurityPrincipalService());

    public SecuritySessionContext ProcessSecuritySessionContext(SecuritySessionContext securitySessionContext) =>
        TryCatch<SecuritySessionContext>(operation: () =>
    {
        ValidateSecuritySessionContext(securitySessionContext: securitySessionContext);
        var state = securitySessionContext.State!;

        foreach (var principal in state.Principals)
        {
            principals.ValidateStoragePrincipalId(storagePrincipalId: principal.Id);
        }

        return securitySessionContext with
        {
            Snapshot = snapshots.PrepareStorageSecuritySnapshot(storageSecuritySnapshot: new StorageSecuritySnapshot { Revision = state.Revision, RetiredPrincipals = state.RetiredPrincipals, DatabasePermissions = state.DatabasePermissions, PrincipalDependencies = state.PrincipalDependencies, Principals = state.Principals, Permissions = state.Permissions, MutationAudit = state.Audit })
        };
    });
}
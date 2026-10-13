// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecuritySnapshots;

namespace sql_storage_engine.Exposures.Security;
/// <summary>Prepares immutable security-catalog snapshots through the snapshot processing contract.</summary>
public static class StorageSecuritySnapshotExposure
{
    private static readonly IStorageSecuritySnapshotService service = new StorageSecuritySnapshotService();
    internal static StorageSecuritySnapshot CreateStorageSecuritySnapshot(StorageSecurityState storageSecurityState) =>
        service.PrepareStorageSecuritySnapshot(storageSecuritySnapshot: new StorageSecuritySnapshot { Revision = storageSecurityState.Revision, RetiredPrincipals = storageSecurityState.RetiredPrincipals, DatabasePermissions = storageSecurityState.DatabasePermissions, PrincipalDependencies = storageSecurityState.PrincipalDependencies, Principals = storageSecurityState.Principals, Permissions = storageSecurityState.Permissions, MutationAudit = storageSecurityState.Audit });

    public static StorageSecuritySnapshot PrepareStorageSecuritySnapshot(StorageSecuritySnapshot storageSecuritySnapshot) =>
        service.PrepareStorageSecuritySnapshot(storageSecuritySnapshot: storageSecuritySnapshot);
}
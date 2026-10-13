// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecuritySnapshots;
internal sealed partial class StorageSecuritySnapshotService : IStorageSecuritySnapshotService
{
    public StorageSecuritySnapshot PrepareStorageSecuritySnapshot(StorageSecuritySnapshot storageSecuritySnapshot) =>
        TryCatch(operation: () =>
    {
        ValidateStorageSecuritySnapshot(storageSecuritySnapshot: storageSecuritySnapshot);

        return new StorageSecuritySnapshot
        {
            Revision = storageSecuritySnapshot.Revision,
            RetiredPrincipals = Array.AsReadOnly(array: storageSecuritySnapshot.RetiredPrincipals.ToArray()),
            DatabasePermissions = Array.AsReadOnly(array: storageSecuritySnapshot.DatabasePermissions.ToArray()),
            PrincipalDependencies = Array.AsReadOnly(array: storageSecuritySnapshot.PrincipalDependencies.ToArray()),
            Principals = Array.AsReadOnly(array: storageSecuritySnapshot.Principals.ToArray()),
            Permissions = Array.AsReadOnly(array: storageSecuritySnapshot.Permissions.ToArray()),
            MutationAudit = Array.AsReadOnly(array: storageSecuritySnapshot.MutationAudit.ToArray())
        };
    });
}
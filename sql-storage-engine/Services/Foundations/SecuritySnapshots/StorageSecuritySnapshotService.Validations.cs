// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecuritySnapshots;
internal sealed partial class StorageSecuritySnapshotService
{
    private static void ValidateStorageSecuritySnapshot(StorageSecuritySnapshot storageSecuritySnapshot) =>
        Validate(inputs: [(storageSecuritySnapshot is null, nameof(storageSecuritySnapshot)), (storageSecuritySnapshot?.RetiredPrincipals is null, nameof(StorageSecuritySnapshot.RetiredPrincipals)), (storageSecuritySnapshot?.DatabasePermissions is null, nameof(StorageSecuritySnapshot.DatabasePermissions)), (storageSecuritySnapshot?.PrincipalDependencies is null, nameof(StorageSecuritySnapshot.PrincipalDependencies)), (storageSecuritySnapshot?.Principals is null, nameof(StorageSecuritySnapshot.Principals)), (storageSecuritySnapshot?.Permissions is null, nameof(StorageSecuritySnapshot.Permissions)), (storageSecuritySnapshot?.MutationAudit is null, nameof(StorageSecuritySnapshot.MutationAudit))]);

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
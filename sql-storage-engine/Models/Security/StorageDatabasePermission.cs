// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
public sealed record StorageDatabasePermission
{
    public StoragePrincipalId Principal { get; init; }
    public StorageDatabasePermissionAction Action { get; init; }
    public StoragePermissionEffect Effect { get; init; }
}
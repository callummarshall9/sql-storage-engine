// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Security;

namespace sql_storage_engine.Models.Security;
public sealed record StoragePermission
{
    public StoragePrincipalId Principal { get; init; }
    public Guid ObjectId { get; init; }
    public StoragePermissionAction Action { get; init; }
    public StoragePermissionEffect Effect { get; init; }
}
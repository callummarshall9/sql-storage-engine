// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Security;
public enum StoragePermissionAction
{
    Select,
    Insert,
    Update,
    Delete,
    ManagePermissions,
    ManagePrincipals
}
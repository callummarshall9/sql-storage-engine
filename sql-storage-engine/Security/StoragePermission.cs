namespace sql_storage_engine.Security;

public sealed record StoragePermission(StoragePrincipalId Principal, Guid ObjectId, StoragePermissionAction Action, StoragePermissionEffect Effect);

namespace sql_storage_engine.Security;

public sealed record StorageRequiredAccess(StorageObjectBinding Object, StoragePermissionAction Action);

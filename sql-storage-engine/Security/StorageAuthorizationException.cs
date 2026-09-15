namespace sql_storage_engine.Security;

public sealed class StorageAuthorizationException(string reason) : InvalidOperationException(reason);

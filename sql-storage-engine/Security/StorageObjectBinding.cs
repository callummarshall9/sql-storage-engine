namespace sql_storage_engine.Security;

public sealed record StorageObjectBinding(sql_storage_engine.Identifiers.DatabaseId DatabaseId, Guid ObjectId, ulong SchemaVersion);

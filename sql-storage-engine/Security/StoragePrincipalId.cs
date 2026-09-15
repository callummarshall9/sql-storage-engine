namespace sql_storage_engine.Security;

public readonly record struct StoragePrincipalId(Guid Issuer, Guid Subject);

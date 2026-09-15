namespace sql_storage_engine.Security;

/// <summary>Host-trusted reference index. Future role/ownership/delegation writers must update this atomically with their records.</summary>
public sealed record StoragePrincipalDependency(StoragePrincipalId Principal, StoragePrincipalDependencyKind Kind, Guid ReferenceId);

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Security;
internal sealed record StorageSecurityState
{
    public long Revision { get; init; }
    public StoragePrincipal[] Principals { get => principals!; init => principals = value; }
    public StoragePermission[] Permissions { get => permissions!; init => permissions = value; }
    public StoragePrincipalId[] RetiredPrincipals { get => retiredPrincipals!; init => retiredPrincipals = value; }
    public StorageDatabasePermission[] DatabasePermissions { get => databasePermissions!; init => databasePermissions = value; }
    public StoragePrincipalReference[] PrincipalDependencies { get => principalDependencies!; init => principalDependencies = value; }
    public StorageAuditRecord[] Audit { get => audit!; init => audit = value; }

    private StoragePrincipal[]? principals;
    private StoragePermission[]? permissions;
    private StoragePrincipalId[]? retiredPrincipals;
    private StorageDatabasePermission[]? databasePermissions;
    private StoragePrincipalReference[]? principalDependencies;
    private StorageAuditRecord[]? audit;
}
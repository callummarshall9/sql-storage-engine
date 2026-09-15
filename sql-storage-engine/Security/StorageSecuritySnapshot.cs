namespace sql_storage_engine.Security;

public sealed class StorageSecuritySnapshot
{
    internal StorageSecuritySnapshot(StorageSecurityState state)
    {
        Revision = state.Revision;
        RetiredPrincipals = Array.AsReadOnly(state.RetiredPrincipals.ToArray());
        DatabasePermissions = Array.AsReadOnly(state.DatabasePermissions.ToArray());
        PrincipalDependencies = Array.AsReadOnly(state.PrincipalDependencies.ToArray());
        Principals = Array.AsReadOnly(state.Principals.ToArray());
        Permissions = Array.AsReadOnly(state.Permissions.ToArray());
        MutationAudit = Array.AsReadOnly(state.Audit.ToArray());
    }
    public IReadOnlyList<StoragePrincipalId> RetiredPrincipals { get; }
    public IReadOnlyList<StorageDatabasePermission> DatabasePermissions { get; }
    public IReadOnlyList<StoragePrincipalDependency> PrincipalDependencies { get; }
    public long Revision { get; }
    public IReadOnlyList<StoragePrincipal> Principals { get; }
    public IReadOnlyList<StoragePermission> Permissions { get; }
    public IReadOnlyList<StorageAuditRecord> MutationAudit { get; }
}

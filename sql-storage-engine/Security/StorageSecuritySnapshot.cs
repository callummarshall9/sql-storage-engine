namespace sql_storage_engine.Security;

public sealed class StorageSecuritySnapshot
{
    internal StorageSecuritySnapshot(StorageSecurityState state)
    {
        Revision = state.Revision;
        Principals = Array.AsReadOnly(state.Principals.ToArray());
        Permissions = Array.AsReadOnly(state.Permissions.ToArray());
        MutationAudit = Array.AsReadOnly(state.Audit.ToArray());
    }
    public long Revision { get; }
    public IReadOnlyList<StoragePrincipal> Principals { get; }
    public IReadOnlyList<StoragePermission> Permissions { get; }
    public IReadOnlyList<StorageAuditRecord> MutationAudit { get; }
}

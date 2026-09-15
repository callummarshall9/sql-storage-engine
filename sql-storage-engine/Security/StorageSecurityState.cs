using System.Text.Json;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Security;

internal sealed partial record StorageSecurityState
{
    public long Revision { get; init; } = 1;
    public StoragePrincipal[] Principals { get; init; } = [];
    public StoragePermission[] Permissions { get; init; } = [];
    public StoragePrincipalId[] RetiredPrincipals { get; init; } = [];
    public StorageDatabasePermission[] DatabasePermissions { get; init; } = [];
    public StoragePrincipalDependency[] PrincipalDependencies { get; init; } = [];
    public StorageAuditRecord[] Audit { get; init; } = [];
    internal static void ValidatePrincipal(StoragePrincipalId principal)
    {
        if (principal.Issuer == Guid.Empty || principal.Subject == Guid.Empty)
            throw new ArgumentException("A principal needs nonempty issuer and subject identities.");
    }
    internal static void ValidateRecord(StorageAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.OperationId == Guid.Empty || record.CorrelationId == Guid.Empty || record.Revision <= 0 ||
            record.ObjectId == Guid.Empty || !Enum.IsDefined(record.Action) || !Enum.IsDefined(record.Kind) || !Enum.IsDefined(record.Reason))
            throw new ArgumentException("Invalid audit record.");
        if (record.Principal is { } principal) ValidatePrincipal(principal);
        if (record.TargetPrincipal is { } target) ValidatePrincipal(target);
        if ((record.TargetPrincipal is null) != (record.PrincipalOperation is null) ||
            record.PrincipalOperation is { } operation && (!Enum.IsDefined(operation) || record.Principal is null ||
                record.Action != StoragePermissionAction.ManagePrincipals || record.Kind != StorageAuditKind.SecurityChange))
            throw new ArgumentException("Invalid principal lifecycle audit.");
        if (JsonSerializer.SerializeToUtf8Bytes(record).Length > 2048) throw new ArgumentException("Audit record exceeds quota.");
    }
    internal static byte[] Encode(StorageSecurityState state) => JsonSerializer.SerializeToUtf8Bytes(state);
    internal static StorageSecurityState Decode(byte[] bytes)
    {
        try
        {
            if (bytes.Length > 4 * 1024 * 1024) throw new ArgumentException();
            var state = JsonSerializer.Deserialize<StorageSecurityState>(bytes) ?? throw new ArgumentException();
            if (state.Revision <= 0 || state.Principals is null || state.Permissions is null || state.Audit is null ||
                state.Principals.Length > 1024 || state.Permissions.Length > 1024 || state.Audit.Length > 4096 ||
                state.Principals.Select(p => p.Id).Distinct().Count() != state.Principals.Length ||
                state.Permissions.Distinct().Count() != state.Permissions.Length ||
                state.Audit.Select(a => a.OperationId).Distinct().Count() != state.Audit.Length) throw new ArgumentException();
            ValidateLifecycle(state);
            foreach (var principal in state.Principals) ValidatePrincipal(principal.Id);
            foreach (var permission in state.Permissions)
            {
                ValidatePrincipal(permission.Principal);
                if (permission.ObjectId == Guid.Empty || !Enum.IsDefined(permission.Action) || permission.Action == StoragePermissionAction.ManagePrincipals || !Enum.IsDefined(permission.Effect)) throw new ArgumentException();
            }
            foreach (var audit in state.Audit)
            {
                ValidateRecord(audit);
                if (audit.Kind is not (StorageAuditKind.Mutation or StorageAuditKind.SecurityChange) || audit.Revision > state.Revision) throw new ArgumentException();
            }
            return state;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        { throw new StorageFormatException("Invalid security catalog."); }
    }
}

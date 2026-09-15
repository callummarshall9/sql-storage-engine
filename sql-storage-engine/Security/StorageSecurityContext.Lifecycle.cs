namespace sql_storage_engine.Security;

internal sealed partial class StorageSecurityContext
{
    public async ValueTask SetDatabasePermissionAsync(StorageDatabasePermission permission, bool remove, long expectedRevision,
        Guid operationId, CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(permission);
            var state = Current(expectedRevision, operationId);
            if (!Enum.IsDefined(permission.Action) || !Enum.IsDefined(permission.Effect) ||
                !state.Principals.Any(p => p.Id == permission.Principal && p.Active)) throw new ArgumentException("Invalid database permission target.");
            var entries = state.DatabasePermissions.Where(p => p != permission).Concat(remove ? [] : new[] { permission }).ToArray();
            if (entries.Length > 1024) throw new InvalidOperationException("Database permission quota exceeded.");
            var next = state with { DatabasePermissions = entries, Revision = state.DatabasePermissions.Contains(permission) != remove ? state.Revision : checked(state.Revision + 1) };
            await PublishAsync(next, new(operationId, operationId, null, owner.DatabaseId.Value, StoragePermissionAction.ManagePrincipals,
                StorageAuditKind.SecurityChange, next.Revision), cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }

    public async ValueTask SetPrincipalDependencyAsync(StoragePrincipalDependency dependency, bool remove, long expectedRevision,
        Guid operationId, CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(dependency);
            var state = Current(expectedRevision, operationId);
            if (!Enum.IsDefined(dependency.Kind) || dependency.ReferenceId == Guid.Empty ||
                !state.Principals.Any(p => p.Id == dependency.Principal)) throw new ArgumentException("Invalid principal dependency.");
            var entries = state.PrincipalDependencies.Where(p => p != dependency).Concat(remove ? [] : new[] { dependency }).ToArray();
            if (entries.Length > 1024) throw new InvalidOperationException("Principal dependency quota exceeded.");
            var next = state with { PrincipalDependencies = entries, Revision = state.PrincipalDependencies.Contains(dependency) != remove ? state.Revision : checked(state.Revision + 1) };
            await PublishAsync(next, new(operationId, operationId, null, owner.DatabaseId.Value, StoragePermissionAction.ManagePrincipals,
                StorageAuditKind.SecurityChange, next.Revision), cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }

    public async ValueTask ChangePrincipalAsync(StoragePrincipalChange change, CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(change);
            StorageSecurityState.ValidatePrincipal(change.Actor); StorageSecurityState.ValidatePrincipal(change.Target);
            var state = Current(change.Revision, change.OperationId);
            if (change.DatabaseId != owner.DatabaseId || change.CorrelationId == Guid.Empty || !Enum.IsDefined(change.Operation))
                throw new ArgumentException("Invalid principal lifecycle request.");
            var authority = state.DatabasePermissions.Where(p => p.Principal == change.Actor && p.Action == StorageDatabasePermissionAction.ManagePrincipals).ToArray();
            if (!state.Principals.Any(p => p.Id == change.Actor && p.Active) || !authority.Any(p => p.Effect == StoragePermissionEffect.Grant) || authority.Any(p => p.Effect == StoragePermissionEffect.Deny))
                throw new StorageAuthorizationException("Database principal administration is denied.");
            if (state.RetiredPrincipals.Contains(change.Target)) throw new InvalidOperationException("Principal identity is retired.");
            var existing = state.Principals.SingleOrDefault(p => p.Id == change.Target);
            StorageSecurityState next;
            switch (change.Operation)
            {
                case StoragePrincipalOperation.Create:
                    if (existing is not null) throw new InvalidOperationException("Principal already exists.");
                    if (state.Principals.Length >= 1024) throw new InvalidOperationException("Principal quota exceeded.");
                    next = state with { Principals = [.. state.Principals, new(change.Target, true)], Revision = checked(state.Revision + 1) };
                    break;
                case StoragePrincipalOperation.Deactivate:
                    if (existing is null) throw new InvalidOperationException("Principal does not exist.");
                    next = state with { Principals = state.Principals.Select(p => p.Id == change.Target ? p with { Active = false } : p).ToArray(), Revision = existing.Active ? checked(state.Revision + 1) : state.Revision };
                    break;
                case StoragePrincipalOperation.Retire:
                    if (existing is null) throw new InvalidOperationException("Principal does not exist.");
                    if (state.PrincipalDependencies.Any(d => d.Principal == change.Target && d.Kind != StoragePrincipalDependencyKind.Membership))
                        throw new InvalidOperationException("Principal has live ownership or delegation dependencies.");
                    if (state.RetiredPrincipals.Length >= 1024) throw new InvalidOperationException("Retirement tombstone quota exceeded.");
                    next = state with { Revision = checked(state.Revision + 1),
                        Principals = state.Principals.Where(p => p.Id != change.Target).ToArray(), RetiredPrincipals = [.. state.RetiredPrincipals, change.Target],
                        Permissions = state.Permissions.Where(p => p.Principal != change.Target).ToArray(),
                        DatabasePermissions = state.DatabasePermissions.Where(p => p.Principal != change.Target).ToArray(),
                        PrincipalDependencies = state.PrincipalDependencies.Where(p => p.Principal != change.Target).ToArray() };
                    break;
                default: throw new ArgumentException("Unknown principal operation.");
            }
            await PublishAsync(next, new(change.OperationId, change.CorrelationId, change.Actor, owner.DatabaseId.Value,
                StoragePermissionAction.ManagePrincipals, StorageAuditKind.SecurityChange, next.Revision)
                { TargetPrincipal = change.Target, PrincipalOperation = change.Operation }, cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }
}

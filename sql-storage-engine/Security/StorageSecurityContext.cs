using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Security;

internal sealed partial class StorageSecurityContext(StorageEngine owner, IStorageTransactionContext context,
    Func<bool> active, Action poison) : IStorageSecurityContext
{
    private void Check()
    {
        if (!active()) throw new InvalidOperationException("The transaction callback has ended.");
        owner.RequireSecurityTransaction();
    }
    public StorageSecuritySnapshot Snapshot { get { Check(); return new(owner.SecurityCatalog.SecurityState); } }
    public StorageObjectBinding ResolveObject(TableId tableId)
    {
        Check();
        // Bootstrap must first persist the format upgrade before legacy identities may escape.
        if (owner.SecurityCatalog.SecurityState.Revision == 1) throw new InvalidOperationException("Provision a principal before resolving security bindings.");
        if (!owner.SecurityCatalog.TryOpenTable(tableId, out var table)) throw new ArgumentException("Unknown object.");
        return new(owner.DatabaseId, table!.ObjectId, table.SchemaVersion);
    }
    public async ValueTask SetPrincipalAsync(StoragePrincipal principal, long expectedRevision, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(principal); StorageSecurityState.ValidatePrincipal(principal.Id);
            var state = Current(expectedRevision, operationId);
            if (state.RetiredPrincipals.Contains(principal.Id)) throw new InvalidOperationException("Principal identity is retired.");
            var principals = state.Principals.Where(p => p.Id != principal.Id).Append(principal).ToArray();
            if (principals.Length > 1024) throw new InvalidOperationException("Principal quota exceeded.");
            var next = state with { Revision = state.Principals.Contains(principal) ? state.Revision : checked(state.Revision + 1), Principals = principals };
            await PublishAsync(next, new(operationId, operationId, null, null, StoragePermissionAction.ManagePermissions,
                StorageAuditKind.SecurityChange, next.Revision), cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }
    public async ValueTask SetPermissionAsync(StoragePermission permission, bool remove, long expectedRevision, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(permission);
            StorageSecurityState.ValidatePrincipal(permission.Principal);
            var state = Current(expectedRevision, operationId);
            if (!Enum.IsDefined(permission.Action) || permission.Action == StoragePermissionAction.ManagePrincipals || !Enum.IsDefined(permission.Effect) ||
                !state.Principals.Any(p => p.Id == permission.Principal && p.Active) ||
                !owner.SecurityCatalog.Tables.Any(t => t.ObjectId == permission.ObjectId)) throw new ArgumentException("Invalid permission target.");
            var permissions = state.Permissions.Where(p => p != permission).Concat(remove ? [] : new[] { permission }).ToArray();
            if (permissions.Length > 1024) throw new InvalidOperationException("Permission quota exceeded.");
            var next = state with { Revision = state.Permissions.Contains(permission) != remove ? state.Revision : checked(state.Revision + 1), Permissions = permissions };
            await PublishAsync(next, new(operationId, operationId, null, permission.ObjectId, StoragePermissionAction.ManagePermissions,
                StorageAuditKind.SecurityChange, next.Revision), cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }
    private StorageSecurityState Current(long expected, Guid operation)
    {
        var state = owner.SecurityCatalog.SecurityState;
        if (expected != state.Revision) throw new StorageAuthorizationException("Security revision is stale.");
        if (operation == Guid.Empty || state.Audit.Any(a => a.OperationId == operation))
            throw new ArgumentException("Invalid or previously recorded operation identity; resolve before retrying.");
        return state;
    }
    private async ValueTask PublishAsync(StorageSecurityState state, StorageAuditRecord record, CancellationToken token)
    {
        StorageSecurityState.ValidateRecord(record);
        if (state.Audit.Length >= 4096) throw new InvalidOperationException("Mutation audit quota exceeded.");
        token.ThrowIfCancellationRequested();
        owner.TransactionObserver?.Invoke("security-mutation-before-audit");
        await owner.SecurityCatalog.WriteSecurityAsync(state with { Audit = [.. state.Audit, record] }, token).ConfigureAwait(false);
        owner.TransactionObserver?.Invoke("security-mutation-after-audit");
    }
    public async ValueTask ExecuteAuthorizedAsync(StorageAuthorizationRequest request,
        Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default)
    {
        Check();
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, true).ConfigureAwait(false);
        using var scope = (StorageGateActivation)owner.ActivateGate(lease);
        Check();
        try
        {
            ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(operation);
            StorageSecurityState.ValidatePrincipal(request.Principal);
            var state = Current(request.Revision, request.OperationId);
            if (request.CorrelationId == Guid.Empty || request.Access.Count is < 1 or > 256)
                throw new ArgumentException("Invalid authorization request.");
            if (!state.Principals.Any(p => p.Id == request.Principal && p.Active)) throw new StorageAuthorizationException("Principal is not active.");
            foreach (var access in request.Access)
            {
                if (access is null || access.Object is null || !Enum.IsDefined(access.Action) || access.Action == StoragePermissionAction.ManagePrincipals) throw new ArgumentException("Invalid required access.");
                if (access.Object.DatabaseId != owner.DatabaseId || !owner.SecurityCatalog.Tables.Any(t =>
                    t.ObjectId == access.Object.ObjectId && t.SchemaVersion == access.Object.SchemaVersion))
                    throw new StorageAuthorizationException("Object binding is stale or foreign.");
                var matches = state.Permissions.Where(p => p.Principal == request.Principal &&
                    p.ObjectId == access.Object.ObjectId && p.Action == access.Action).ToArray();
                if (!matches.Any(p => p.Effect == StoragePermissionEffect.Grant) || matches.Any(p => p.Effect == StoragePermissionEffect.Deny))
                    throw new StorageAuthorizationException("Required permission is denied.");
            }
            var read = request.Access.All(a => a.Action == StoragePermissionAction.Select);
            var first = request.Access[0];
            var record = new StorageAuditRecord(request.OperationId, request.CorrelationId, request.Principal,
                request.Access.Select(a => a.Object.ObjectId).Distinct().Count() == 1 ? first.Object.ObjectId : null,
                read ? StoragePermissionAction.Select : request.Access.First(a => a.Action != StoragePermissionAction.Select).Action,
                read ? StorageAuditKind.ReadAdmission : StorageAuditKind.Mutation, state.Revision);
            if (read) await owner.Security.AppendEventAsync(record, cancellationToken).ConfigureAwait(false);
            await operation(context, cancellationToken).ConfigureAwait(false);
            await scope.DrainAsync().ConfigureAwait(false);
            Check(); cancellationToken.ThrowIfCancellationRequested();
            // The callback may not administer the security catalog while using this admission.
            if (owner.SecurityCatalog.SecurityState != state) throw new StorageAuthorizationException("Security changed inside an admitted operation.");
            if (!read) await PublishAsync(state, record, cancellationToken).ConfigureAwait(false);
        }
        catch { poison(); throw; }
    }
}

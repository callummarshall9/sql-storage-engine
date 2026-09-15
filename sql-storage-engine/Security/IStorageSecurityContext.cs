using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Security;

/// <summary>Host-trusted administration and admission inside an exclusive storage transaction callback.</summary>
public interface IStorageSecurityContext
{
    /// <summary>Host-trusted bootstrap only; object ManagePermissions never provisions database authority.</summary>
    ValueTask SetDatabasePermissionAsync(StorageDatabasePermission permission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException("Principal lifecycle is unavailable."));
    /// <summary>Host-trusted reference maintenance; not an API to grant roles, ownership or delegation.</summary>
    ValueTask SetPrincipalDependencyAsync(StoragePrincipalDependency dependency, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException("Principal lifecycle is unavailable."));
    /// <summary>Requires active actor and database ManagePrincipals; change and actor audit share the enclosing root.</summary>
    ValueTask ChangePrincipalAsync(StoragePrincipalChange change, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException("Principal lifecycle is unavailable."));
    StorageSecuritySnapshot Snapshot { get; }
    StorageObjectBinding ResolveObject(TableId tableId);
    ValueTask SetPrincipalAsync(StoragePrincipal principal, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);
    ValueTask SetPermissionAsync(StoragePermission permission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Runs only after every requested access is admitted. The root lease protects admission until root end.
    /// Mutation audit becomes durable only with the enclosing transaction's committed receipt.</summary>
    ValueTask ExecuteAuthorizedAsync(StorageAuthorizationRequest request,
        Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default);
}

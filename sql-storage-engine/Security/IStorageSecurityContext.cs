// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Security;
/// <summary>Host-trusted administration and admission inside an exclusive storage transaction callback.</summary>
public interface IStorageSecurityContext
{
    /// <summary>Host-trusted bootstrap only; object ManagePermissions never provisions database authority.</summary>
    ValueTask SetDatabasePermissionAsync(StorageDatabasePermission storageDatabasePermission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Host-trusted reference maintenance; not an API to grant roles, ownership or delegation.</summary>
    ValueTask SetPrincipalDependencyAsync(StoragePrincipalReference storagePrincipalReference, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Requires active actor and database ManagePrincipals; change and actor audit share the enclosing root.</summary>
    ValueTask ChangePrincipalAsync(StoragePrincipalChange storagePrincipalChange, CancellationToken cancellationToken = default);

    StorageSecuritySnapshot ReadSnapshot();

    StorageObjectBinding ResolveObject(TableId tableId);

    ValueTask SetPrincipalAsync(StoragePrincipal storagePrincipal, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);

    ValueTask SetPermissionAsync(StoragePermission storagePermission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Runs only after every requested access is admitted. The root lease protects admission until root end.
    /// Mutation audit becomes durable only with the enclosing transaction's committed receipt.</summary>
    ValueTask ExecuteAuthorizedAsync(StorageAuthorizationRequest storageAuthorizationRequest, Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default);
}
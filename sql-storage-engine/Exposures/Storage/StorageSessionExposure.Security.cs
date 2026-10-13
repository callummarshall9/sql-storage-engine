// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Security;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure : IStorageSecurity, IStorageSecurityContext
{
    public IStorageSecurity ReadSecurity() =>
        new StorageSessionExposure(storageSessionContext: sessionContext);

    IStorageSecurityContext IStorageTransactionContext.ReadSecurity() =>
        new StorageSessionExposure(storageSessionContext: sessionContext);

    public async ValueTask<StorageSecuritySnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Binding = sessionContext.Binding, Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 10 } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).SecurityRequest!.SnapshotResult!;

    public StorageSecuritySnapshot ReadSnapshot() =>
        service.ReadStorageSessionContextSecuritySnapshot(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext() })
            .SecurityRequest!.SnapshotResult!;

    public StorageObjectBinding ResolveObject(TableId tableId) =>
        service.ReadStorageSessionContextSecurityObject(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { TableId = tableId } })
            .SecurityRequest!.ObjectResult!;

    public ValueTask AppendEventAsync(StorageAuditRecord storageAuditRecord, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, SecurityRequest = new StorageSecurityRequestContext { Operation = 8, Record = storageAuditRecord } }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<IReadOnlyList<StorageAuditRecord>> ReadEventsAsync(CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, SecurityRequest = new StorageSecurityRequestContext { Operation = 9 } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).SecurityRequest!.EventsResult!;

    public ValueTask ExecuteAuthorizedAsync(StorageAuthorizationRequest storageAuthorizationRequest, Func<IStorageTransactionContext, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Binding = sessionContext.Binding, Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 6, Authorization = storageAuthorizationRequest, CallbackSource = operation, Callback = (context, token) => operation(arg1: new StorageSessionExposure(storageSessionContext: context), arg2: token) } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask SetPrincipalAsync(StoragePrincipal storagePrincipal, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 1, Mutation = new SecurityCatalogMutation { Principal = storagePrincipal, ExpectedRevision = expectedRevision, OperationId = operationId } } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask SetPermissionAsync(StoragePermission storagePermission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 2, Mutation = new SecurityCatalogMutation { Permission = storagePermission, ExpectedRevision = expectedRevision, OperationId = operationId, Remove = remove } } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask SetDatabasePermissionAsync(StorageDatabasePermission storageDatabasePermission, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 3, Mutation = new SecurityCatalogMutation { DatabasePermission = storageDatabasePermission, ExpectedRevision = expectedRevision, OperationId = operationId, Remove = remove } } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask SetPrincipalDependencyAsync(StoragePrincipalReference storagePrincipalReference, bool remove, long expectedRevision, Guid operationId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 4, Mutation = new SecurityCatalogMutation { PrincipalDependency = storagePrincipalReference, ExpectedRevision = expectedRevision, OperationId = operationId, Remove = remove } } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask ChangePrincipalAsync(StoragePrincipalChange storagePrincipalChange, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextSecurityAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, SecurityRequest = new StorageSecurityRequestContext { Operation = 5, Mutation = new SecurityCatalogMutation { PrincipalChange = storagePrincipalChange } } }, cancellationToken: cancellationToken)
            .AsTask());

    internal StorageSecurityState ReadStorageSecurityState() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new sql_storage_engine.Models.Catalog.CatalogSessionContext() })
            .Catalog!.Definition!.Security;

    internal ValueTask WriteSecurityAsync(StorageSecurityState storageSecurityState, CancellationToken token) =>
        new ValueTask(task: service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new sql_storage_engine.Models.Catalog.CatalogSessionContext { Operation = 29, State = storageSecurityState } }, cancellationToken: token)
            .AsTask());
}
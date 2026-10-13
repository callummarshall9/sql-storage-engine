// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed class StorageSecurityRequestContext
{
    public int Operation { get; init; }
    public TableId TableId { get; init; }
    public SecurityCatalogMutation? Mutation { get; init; }
    public StorageAuthorizationRequest? Authorization { get; init; }
    public Func<StorageSessionContext, CancellationToken, ValueTask>? Callback { get; init; }
    public StorageAuditRecord? Record { get; init; }
    public StorageObjectBinding? ObjectResult { get; set; }
    public StorageSecuritySnapshot? SnapshotResult { get; set; }
    public IReadOnlyList<StorageAuditRecord>? EventsResult { get; set; }
    public Func<IStorageTransactionContext, CancellationToken, ValueTask>? CallbackSource { get; init; }
}
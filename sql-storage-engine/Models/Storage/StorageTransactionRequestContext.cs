// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageTransactionRequestContext
{
    public object? CallbackSource { get; init; }
    public string? Operation { get; init; }
    public StorageTransactionOptions? Options { get; init; }
    public StorageTransactionIdentity Identity { get; init; }
    public string? Name { get; init; }
    public StorageSavepoint? Savepoint { get; init; }
    public Func<StorageSessionContext, CancellationToken, ValueTask>? Callback { get; init; }
    public StorageTransactionReceipt? Receipt { get; set; }
    public StorageSavepoint? SavepointResult { get; set; }
    public StorageSessionContext? Result { get; set; }
}
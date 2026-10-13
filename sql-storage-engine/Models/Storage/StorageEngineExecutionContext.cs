// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageEngineExecutionContext
{
    public object? CallbackSource { get; init; }
    public string? Operation { get; init; }
    public Func<StorageSessionContext, CancellationToken, ValueTask>? OperationCallback { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Access;
internal sealed class StorageAccessScopeState
{
    public object? Sync;
    public object? Owner;
    public bool Active;
    public List<IAsyncDisposable>? Resources;
    public TaskCompletionSource? Child;
    public Task? Drain;
}
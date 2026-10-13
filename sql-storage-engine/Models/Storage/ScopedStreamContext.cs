// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Storage;
internal sealed record ScopedStreamContext
{
    public object? Source { get; init; }
    public Guid LeaseId { get; init; }
    public IAsyncDisposable? Resource { get; init; }
}
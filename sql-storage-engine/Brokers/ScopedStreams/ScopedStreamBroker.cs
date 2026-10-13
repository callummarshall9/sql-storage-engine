// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Dependencies.ScopedStreams;

namespace sql_storage_engine.Brokers.ScopedStreams;
internal sealed class ScopedStreamBroker<T> : IScopedStreamBroker<T>
{
    private readonly AsyncStreamResourceDependency<T> dependency = new();
    public (Guid LeaseId, IAsyncEnumerable<T> Source, IAsyncDisposable Resource) Open(IAsyncEnumerable<T> source) =>
        dependency.Open(source: source);
}
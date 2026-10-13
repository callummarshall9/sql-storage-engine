// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Brokers.ScopedStreams;
internal interface IScopedStreamBroker<T>
{
    (Guid LeaseId, IAsyncEnumerable<T> Source, IAsyncDisposable Resource) Open(IAsyncEnumerable<T> source);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.OverflowPages;
internal interface IOverflowPageResourceBroker
{
    int ReadPageSize();

    ValueTask<byte[]> ReadAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask<ulong> AllocateAsync(CancellationToken cancellationToken);

    ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FreeAsync(ulong pageId, CancellationToken cancellationToken);
}
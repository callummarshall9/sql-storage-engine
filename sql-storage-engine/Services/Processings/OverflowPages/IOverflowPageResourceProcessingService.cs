// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Processings.OverflowPages;
internal interface IOverflowPageResourceProcessingService
{
    int ReadOverflowPageResourceSize();

    ValueTask<byte[]> ReadOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask WriteOverflowPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask<ulong> AllocateOverflowPageResourceAsync(CancellationToken cancellationToken);

    ValueTask DiscardOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FreeOverflowPageResourceAsync(ulong pageId, CancellationToken cancellationToken);
}
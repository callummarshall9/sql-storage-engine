// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.CatalogBuffers;
internal interface ICatalogBufferBroker
{
    ValueTask FlushAsync(CancellationToken cancellationToken);

    ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask EnterAsync(CancellationToken cancellationToken);

    void Exit();
}
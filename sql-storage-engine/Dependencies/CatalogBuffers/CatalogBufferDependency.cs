// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.CatalogBuffers;
internal sealed class CatalogBufferDependency : IDisposable
{
    private readonly Func<CancellationToken, ValueTask> flush;
    private readonly Func<ulong, CancellationToken, ValueTask> discard;
    private readonly SemaphoreSlim gate = new(initialCount: 1, maxCount: 1);
    public CatalogBufferDependency(Func<CancellationToken, ValueTask> flush, Func<ulong, CancellationToken, ValueTask> discard)
    {
        this.flush = flush;
        this.discard = discard;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        flush(cancellationToken);

    public ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken) =>
        discard(pageId, cancellationToken);

    public async ValueTask EnterAsync(CancellationToken cancellationToken) =>
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

    public void Exit() =>
        gate.Release();

    public void Dispose() =>
        gate.Dispose();
}
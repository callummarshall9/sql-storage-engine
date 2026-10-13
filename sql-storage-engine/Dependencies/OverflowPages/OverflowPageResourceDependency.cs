// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.OverflowPages;
internal sealed class OverflowPageResourceDependency(Func<int> pageSize, Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>> pin, Func<CancellationToken, ValueTask<ulong>> allocate, Func<ulong, CancellationToken, ValueTask> discard, Func<ulong, CancellationToken, ValueTask> free) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public int ReadPageSize() =>
        pageSize();

    public async ValueTask<byte[]> ReadAsync(ulong pageId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var page = await pin(arg1: pageId, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            using var lease = page.Lease;
            return page.Memory.ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var page = await pin(arg1: pageId, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            using var lease = page.Lease;

            if (source.Length != page.Memory.Length)
            {
                throw new ArgumentException(message: "Overflow image length does not match its pin.");
            }

            source.CopyTo(destination: page.Memory);
            page.MarkDirty(obj: 0);
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask<ulong> AllocateAsync(CancellationToken cancellationToken) =>
        allocate(arg: cancellationToken);

    public ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken) =>
        discard(arg1: pageId, arg2: cancellationToken);

    public ValueTask FreeAsync(ulong pageId, CancellationToken cancellationToken) =>
        free(arg1: pageId, arg2: cancellationToken);

    public void Dispose() =>
        gate.Dispose();
}
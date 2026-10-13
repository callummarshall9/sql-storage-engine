// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.HeapResources;
internal sealed class HeapPageResourceDependency(Func<int> pageSize, Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>> pin, Func<CancellationToken, ValueTask<ulong>> allocate, Func<ulong, CancellationToken, ValueTask> discard, Func<ulong, CancellationToken, ValueTask> free, Func<int, ulong?>? find, Action<ulong, int>? update, Action<ulong>? remove, Action? clear) : IDisposable
{
    private readonly Dictionary<Guid, (IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)> leases = new();
    private readonly object leaseSync = new();
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

    public async ValueTask<(Guid Id, Memory<byte> Memory)> AcquireAsync(ulong pageId, CancellationToken cancellationToken)
    {
        var acquired = await pin(arg1: pageId, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var id = Guid.NewGuid();

        lock (leaseSync)
        {
            leases.Add(key: id, value: acquired);
        }

        return (id, acquired.Memory);
    }

    public void MarkDirty(Guid leaseId, ulong logSequenceNumber)
    {
        lock (leaseSync)
        {
            if (!leases.TryGetValue(key: leaseId, value: out var acquired))
            {
                throw new ObjectDisposedException(objectName: nameof(leaseId));
            }

            acquired.MarkDirty(obj: logSequenceNumber);
        }
    }

    public void Release(Guid leaseId)
    {
        IDisposable? lease = null;

        lock (leaseSync)
        {
            if (leases.Remove(key: leaseId, value: out var acquired))
            {
                lease = acquired.Lease;
            }
        }

        lease?.Dispose();
    }

    public long ReadRandomSeed() =>
        Random.Shared.NextInt64();

    public ulong? Find(int requiredBytes) =>
        find!(arg: requiredBytes);

    public void Update(ulong pageId, int freeBytes) =>
        update!(arg1: pageId, arg2: freeBytes);

    public void Remove(ulong pageId) =>
        remove!(obj: pageId);

    public void Clear() =>
        clear!();

    public void Dispose()
    {
        lock (leaseSync)
        {
            foreach (var acquired in leases.Values)
            {
                acquired.Lease.Dispose();
            }

            leases.Clear();
        }

        gate.Dispose();
    }
}
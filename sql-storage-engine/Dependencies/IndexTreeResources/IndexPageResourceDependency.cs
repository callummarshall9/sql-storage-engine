// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.IndexTreeResources;
internal sealed class IndexPageResourceDependency(Func<int> pageSize, Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>> pin, Func<ushort, CancellationToken, ValueTask<ulong>> allocate) : IDisposable
{
    private readonly Dictionary<Guid, (IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)> leases = new();
    private readonly object leaseSync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public int ReadPageSize() =>
        pageSize();

    public ValueTask<ulong> AllocateAsync(ushort pageType, CancellationToken cancellationToken) =>
        allocate(arg1: pageType, arg2: cancellationToken);

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
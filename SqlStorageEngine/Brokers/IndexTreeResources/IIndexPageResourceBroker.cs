// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.IndexTreeResources;
internal interface IIndexPageResourceBroker
{
    int ReadPageSize();

    ValueTask<ulong> AllocateAsync(ushort pageType, CancellationToken cancellationToken);

    ValueTask<(Guid Id, Memory<byte> Memory)> AcquireAsync(ulong pageId, CancellationToken cancellationToken);

    void MarkDirty(Guid leaseId, ulong logSequenceNumber);

    void Release(Guid leaseId);
}
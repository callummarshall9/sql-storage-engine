// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Exposures.Buffers;
public interface IBufferPoolExposure
{
    int ReadPageSize();

    ValueTask<IPinnedPageExposure> GetPageAsync(PageId pageId, CancellationToken cancellationToken = default);

    ValueTask FlushAllAsync(CancellationToken cancellationToken = default);

    ValueTask DiscardPageAsync(PageId pageId, CancellationToken cancellationToken = default);

    ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)> GetPageLeaseAsync(PageId pageId, CancellationToken cancellationToken = default);
}
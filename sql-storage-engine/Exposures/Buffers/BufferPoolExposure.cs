// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using cCoder.CodeAnalysis.Exposures;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Buffers;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Orchestrations.BufferPools;

namespace sql_storage_engine.Exposures.Buffers;
public sealed class BufferPoolExposure : IAsyncDisposable, IPinnedPageExposure, IBufferPoolExposure, ICompositionExposure
{
    private readonly BufferPoolContextOrchestrationService service;
    private readonly BufferPoolContext? pinContext;
    public BufferPoolExposure(IPageStoreExposure pageStore, int capacity, bool leaveOpen = false, IPageFlushGuard? flushGuard = null)
    {
        var configuration = new BufferPoolConfiguration
        {
            ProviderSource = pageStore,
            Capacity = capacity,
            LeaveOpen = leaveOpen,
            PageSize = () => pageStore.GetPageSize(),
            Read = (pageId, destination, token) => pageStore.ReadAsync(pageId: new PageId { Value = pageId }, destination: destination, cancellationToken: token),
            Write = (pageId, source, token) => pageStore.WriteAsync(pageId: new PageId { Value = pageId }, source: source, cancellationToken: token),
            Flush = token => pageStore.FlushAsync(cancellationToken: token),
            Close = () => pageStore.DisposeAsync(),
            GuardPresent = flushGuard is not null,
            Guard = (pageId, lsn, token) => flushGuard!.EnsureCanFlushAsync(pageId: new PageId { Value = pageId }, pageLogSequenceNumber: new LogSequenceNumber { Value = lsn }, cancellationToken: token)
        };
        service = BufferPoolContextOrchestrationService.CreateBufferPoolContextOrchestrationService(bufferPoolConfiguration: configuration);
    }

    public int ReadCapacity() =>
        service.ReadBufferPoolContext()
            .Configuration!.Capacity;

    public int ReadPageSize() =>
        service.ReadBufferPoolContext()
            .PageSize;

    public int ReadFrameCount() =>
        service.ReadBufferPoolContextCache()
            .FrameCount;

    public int ReadPinnedPageCount() =>
        service.ReadBufferPoolContextCache()
            .PinnedPageCount;

    public long ReadHitCount() =>
        service.ReadBufferPoolContextMetrics()
            .HitCount;

    public long ReadMissCount() =>
        service.ReadBufferPoolContextMetrics()
            .MissCount;

    public async ValueTask<IPinnedPageExposure> GetPageAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        var context = await service.ReadBufferPoolContextPageAsync(pageId: pageId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new BufferPoolExposure(service: service, bufferPoolContext: context);
    }

    public ValueTask FlushPageAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        service.FlushBufferPoolContextPageAsync(pageId: pageId.Value, cancellationToken: cancellationToken);

    public ValueTask FlushAllAsync(CancellationToken cancellationToken = default) =>
        service.FlushBufferPoolContextAsync(cancellationToken: cancellationToken);

    public ValueTask DiscardPageAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        service.DiscardBufferPoolContextPageAsync(pageId: pageId.Value, cancellationToken: cancellationToken);

    internal ValueTask DiscardAllAsync(CancellationToken cancellationToken = default) =>
        service.DiscardBufferPoolContextAsync(cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() =>
        service.CloseBufferPoolContextAsync(bufferPoolContext: pinContext);
    private BufferPoolExposure(BufferPoolContextOrchestrationService service, BufferPoolContext? bufferPoolContext)
    {
        this.service = service;
        pinContext = bufferPoolContext;
    }

    PageId IPinnedPageExposure.ReadPageId() =>
        new PageId
    {
        Value = service.ReadBufferPoolContextPinPageId(bufferPoolContext: pinContext!)
    };

    Memory<byte> IPinnedPageExposure.ReadMemory() =>
        service.ReadBufferPoolContextPinMemory(bufferPoolContext: pinContext!);

    LogSequenceNumber IPinnedPageExposure.ReadPageLogSequenceNumber() =>
        new LogSequenceNumber
    {
        Value = service.ReadBufferPoolContextPinLogSequenceNumber(bufferPoolContext: pinContext!)
    };

    bool IPinnedPageExposure.ReadIsDirty() =>
        service.ReadBufferPoolContextPinIsDirty(bufferPoolContext: pinContext!);

    void IPinnedPageExposure.MarkDirty(LogSequenceNumber logSequenceNumber) =>
        service.MarkBufferPoolContextPinDirty(bufferPoolContext: pinContext!, logSequenceNumber: logSequenceNumber.Value);

    void IDisposable.Dispose() =>
        service.ReleaseBufferPoolContextPin(bufferPoolContext: pinContext!);

    public async ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)> GetPageLeaseAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        var context = await service.ReadBufferPoolContextPageAsync(pageId: pageId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var lease = new BufferPoolExposure(service: service, bufferPoolContext: context);
        return ((IDisposable)lease, context.ResultFrame!.Bytes!, lease.MarkDirtyAt);
    }

    private void MarkDirtyAt(ulong logSequenceNumber) =>
        service.MarkBufferPoolContextPinDirty(bufferPoolContext: pinContext!, logSequenceNumber: logSequenceNumber);

    internal static BufferPoolExposure BindBufferPoolExposure(object source) =>
        new(service: (BufferPoolContextOrchestrationService)source, bufferPoolContext: null);
}
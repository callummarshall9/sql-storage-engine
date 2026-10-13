// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Orchestrations.OverflowChains;

namespace sql_storage_engine.Exposures.Overflow;
public sealed class OverflowChainExposure : ICompositionExposure, IOverflowChainExposure
{
    private readonly OverflowChainContextOrchestrationService service;
    public OverflowChainExposure(IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator)
    {
        service = OverflowChainContextOrchestrationService.CreateOverflowChainContextOrchestrationService(overflowPageResourceConfiguration: new OverflowPageResourceConfiguration { BufferSource = bufferPool, AllocatorSource = allocator, PageSize = () => bufferPool.ReadPageSize(), Pin = (pageId, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = pageId }, cancellationToken: token), Allocate = async token => (await allocator.AllocateAsync(pageType: PageType.Overflow, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value, Discard = (pageId, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = pageId }, cancellationToken: token), Free = (pageId, token) => allocator.FreeAsync(pageId: new PageId { Value = pageId }, cancellationToken: token) });
    }

    public async ValueTask<OverflowReference> WriteAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
        (await service.WriteOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Value = value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Reference!.Value;

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default) =>
        (await service.ReadOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Reference = overflowReference }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Value;

    public ValueTask FreeAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default) =>
        service.FreeOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Reference = overflowReference }, cancellationToken: cancellationToken);

    public OverflowChainBinding ReadOverflowChainBinding() =>
        new OverflowChainBinding
    {
        Service = service
    };
}
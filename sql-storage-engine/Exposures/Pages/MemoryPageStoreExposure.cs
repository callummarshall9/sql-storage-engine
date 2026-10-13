// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Processings.MemoryPages;

namespace sql_storage_engine.Exposures.Pages;
public sealed class MemoryPageStoreExposure : sql_storage_engine.Exposures.Pages.IPageStoreExposure, sql_storage_engine.Exposures.Pages.IPageAllocatorExposure
{
    private readonly PageHeaderMemoryPageIdProcessingService service;
    public MemoryPageStoreExposure(int pageSize = sql_storage_engine.Models.Pages.PageConstants.DefaultSize, bool reservePageZero = true)
    {
        service = new PageHeaderMemoryPageIdProcessingService(configuration: new MemoryPageConfiguration { PageSize = pageSize, ReservePageZero = reservePageZero });
    }

    public int GetPageSize() =>
        service.ReadMemoryPageSize();

    public ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default) =>
        service.AllocateMemoryPageIdAsync(pageType: pageType, cancellationToken: cancellationToken);

    public ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        service.FreeMemoryPageAsync(pageId: pageId.Value, cancellationToken: cancellationToken);

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default) =>
        service.ReadMemoryPageAsync(pageId: pageId.Value, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        service.WriteMemoryPageAsync(pageId: pageId.Value, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        service.FlushMemoryPagesAsync(cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() =>
        service.CloseMemoryPagesAsync();
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Services.Orchestrations.CatalogPages;

namespace sql_storage_engine.Exposures.Catalog;
public sealed class CatalogPageChainExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private readonly CatalogPageChainOrchestrationService service;
    public const int ChainHeaderLength = sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength + 16;
    public const int MaximumPages = 65_536;
    public CatalogPageChainExposure(IPageStoreExposure pageStore, IPageAllocatorExposure allocator)
    {
        service = CatalogPageChainOrchestrationService.CreateCatalogPageChainOrchestrationService(configuration: new CatalogPageResourceConfiguration { PageSize = pageStore.GetPageSize, Allocate = async (type, token) => (await allocator.AllocateAsync(pageType: (PageType)type, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value, Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token), Read = (id, bytes, token) => pageStore.ReadAsync(pageId: new PageId { Value = id }, destination: bytes, cancellationToken: token), Write = (id, bytes, token) => pageStore.WriteAsync(pageId: new PageId { Value = id }, source: bytes, cancellationToken: token) });
    }

    public ValueTask<byte[]> ReadAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        ReadResultAsync(operation: service.ExecuteCatalogPageChainRequestAsync(catalogPageChainRequest: new CatalogPageChainRequest { RootPageId = pageId }, token: cancellationToken));

    public ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteAsync(byte[] catalogPayload, CancellationToken cancellationToken = default) =>
        WriteResultAsync(operation: service.ExecuteCatalogPageChainRequestAsync(catalogPageChainRequest: new CatalogPageChainRequest { Payload = catalogPayload, IsWrite = true }, token: cancellationToken));

    private static async ValueTask<byte[]> ReadResultAsync(ValueTask<CatalogPageChainRequest> operation) =>
        (await operation.ConfigureAwait(continueOnCapturedContext: false)).Payload!;

    private static async ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteResultAsync(ValueTask<CatalogPageChainRequest> operation) =>
        ProjectWriteResult(catalogPageChainRequest: await operation.ConfigureAwait(continueOnCapturedContext: false));

    private static (PageId RootPageId, IReadOnlyList<PageId> PageIds) ProjectWriteResult(CatalogPageChainRequest catalogPageChainRequest) =>
        (catalogPageChainRequest.RootPageId, catalogPageChainRequest.PageIds!);
}
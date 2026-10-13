// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.CatalogPages;

namespace sql_storage_engine.Services.Foundations.CatalogPages;
internal sealed partial class CatalogPageResourceService(CatalogPageResourceBroker broker) : ICatalogPageResourceService
{
    internal static CatalogPageResourceService CreateCatalogPageResourceService(CatalogPageResourceConfiguration configuration) =>
        new CatalogPageResourceService(broker: new CatalogPageResourceBroker(configuration: configuration));

    public int ReadCatalogPageSize() =>
        TryCatch(operation: () =>
    {
        ValidateReadCatalogPageSize();
        return broker.ReadCatalogPageSize();
    });

    public ValueTask<ulong> AllocateCatalogPageAsync(ushort pageType, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateAllocateCatalogPageAsync(pageType: pageType, token: token);

        return await broker.AllocateCatalogPageAsync(pageType: pageType, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FreeCatalogPageAsync(ulong pageId, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateFreeCatalogPageAsync(pageId: pageId, token: token);

        await broker.FreeCatalogPageAsync(pageId: pageId, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask ReadCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogPageBufferAsync(buffer: catalogPageBuffer, token: token);

        await broker.ReadCatalogPageBufferAsync(catalogPageBuffer: catalogPageBuffer, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogPageBufferAsync(buffer: catalogPageBuffer, token: token);

        await broker.WriteCatalogPageBufferAsync(catalogPageBuffer: catalogPageBuffer, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}
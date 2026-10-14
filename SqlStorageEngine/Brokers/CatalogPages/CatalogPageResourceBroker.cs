// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Dependencies.CatalogPages;

namespace sql_storage_engine.Brokers.CatalogPages;
internal sealed class CatalogPageResourceBroker : ICatalogPageResourceBroker
{
    private readonly CatalogPageResourceDependency dependency;
    public CatalogPageResourceBroker(CatalogPageResourceConfiguration configuration)
    {
        dependency = new CatalogPageResourceDependency(pageSize: configuration.PageSize!, allocate: configuration.Allocate!, free: configuration.Free!, read: configuration.Read!, write: configuration.Write!);
    }

    public int ReadCatalogPageSize() =>
        dependency.ReadPageSize();

    public ValueTask<ulong> AllocateCatalogPageAsync(ushort pageType, CancellationToken token) =>
        dependency.AllocateAsync(pageType: pageType, token: token);

    public ValueTask FreeCatalogPageAsync(ulong pageId, CancellationToken token) =>
        dependency.FreeAsync(pageId: pageId, token: token);

    public ValueTask ReadCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token) =>
        dependency.ReadAsync(pageId: catalogPageBuffer.PageId, bytes: catalogPageBuffer.Bytes, token: token);

    public ValueTask WriteCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token) =>
        dependency.WriteAsync(pageId: catalogPageBuffer.PageId, bytes: catalogPageBuffer.Bytes, token: token);
}
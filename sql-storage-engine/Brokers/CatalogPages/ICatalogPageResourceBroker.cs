// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Brokers.CatalogPages;
internal interface ICatalogPageResourceBroker
{
    int ReadCatalogPageSize();

    ValueTask<ulong> AllocateCatalogPageAsync(ushort pageType, CancellationToken token);

    ValueTask FreeCatalogPageAsync(ulong pageId, CancellationToken token);

    ValueTask ReadCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token);

    ValueTask WriteCatalogPageBufferAsync(CatalogPageBuffer catalogPageBuffer, CancellationToken token);
}
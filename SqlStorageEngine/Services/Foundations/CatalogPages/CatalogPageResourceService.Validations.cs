// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogPages;
internal sealed partial class CatalogPageResourceService
{
    private static void ValidateReadCatalogPageSize()
    {
    }

    private static void ValidateAllocateCatalogPageAsync(ushort pageType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
    }

    private static void ValidateFreeCatalogPageAsync(ulong pageId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
    }

    private static void ValidateCatalogPageBufferAsync(CatalogPageBuffer buffer, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(argument: buffer);
        ArgumentNullException.ThrowIfNull(argument: buffer.Bytes);
        token.ThrowIfCancellationRequested();
    }
}
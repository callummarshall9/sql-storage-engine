// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogBuffers;
internal sealed partial class CatalogBufferService
{
    private static void ValidateCatalogBufferContext(CatalogBufferContext catalogBufferContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogBufferContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (catalogBufferContext.Operation is < 1 or > 4 || (catalogBufferContext.Operation == 2 && catalogBufferContext.PageId == 0))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogBufferContext));
        }
    }
}
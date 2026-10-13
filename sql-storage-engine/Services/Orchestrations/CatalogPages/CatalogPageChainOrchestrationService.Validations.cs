// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using System;
using System.Threading;

namespace sql_storage_engine.Services.Orchestrations.CatalogPages;
internal sealed partial class CatalogPageChainOrchestrationService
{
    private static void ValidateRequest(CatalogPageChainRequest catalogPageChainRequest, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogPageChainRequest);

        if (catalogPageChainRequest.IsWrite)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogPageChainRequest.Payload);
        }

        token.ThrowIfCancellationRequested();
    }
}
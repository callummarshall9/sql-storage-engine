// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogStorage;
internal sealed partial class CatalogStorageLifecycleOrchestrationService
{
    private static void ValidateCatalogStorageContext(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (catalogStorageContext.Operation is < 10 or > 13 || (catalogStorageContext.Operation == 11 && catalogStorageContext.RootPageId.Value == 0))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogStorageContext));
        }
    }
}
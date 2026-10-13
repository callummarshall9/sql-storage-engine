// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Managements.CatalogStorage;
internal sealed partial class CatalogStorageManagementService
{
    private static void ValidateCatalogStorageContext(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (catalogStorageContext.Operation is < 1 or > 15)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogStorageContext));
        }

        if (catalogStorageContext.Operation == 3)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.HeapBinding);
        }

        if (catalogStorageContext.Operation is 5 or 7)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.TreeBinding);
        }

        if (catalogStorageContext.Operation == 5)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.Tree);
        }
    }

    private static void ValidateCatalogStorageResetContext(CatalogStorageContext catalogStorageContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);
}
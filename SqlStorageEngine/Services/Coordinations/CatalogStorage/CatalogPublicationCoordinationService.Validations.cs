// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogStorage;
internal sealed partial class CatalogPublicationCoordinationService
{
    private static void ValidateCatalogStorageContext(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (catalogStorageContext.Operation is < 8 or > 13)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogStorageContext));
        }

        if (catalogStorageContext.Operation == 8)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.Payload);
        }
    }
}
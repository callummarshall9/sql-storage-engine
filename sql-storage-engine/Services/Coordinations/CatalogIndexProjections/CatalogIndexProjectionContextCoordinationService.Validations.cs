// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogIndexProjections;
internal sealed partial class CatalogIndexProjectionContextCoordinationService
{
    private static void ValidateCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext);

        if (catalogIndexProjectionContext.Operation is (< 1 or > 8) and not (16 or 17 or 18))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogIndexProjectionContext));
        }
    }
}
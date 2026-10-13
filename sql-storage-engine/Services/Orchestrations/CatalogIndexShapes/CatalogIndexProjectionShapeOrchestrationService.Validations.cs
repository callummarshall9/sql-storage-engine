// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
internal sealed partial class CatalogIndexProjectionShapeOrchestrationService
{
    private static void ValidateCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext);

        if (catalogIndexProjectionContext.Operation is (< 6 or > 8) and not (17 or 18))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogIndexProjectionContext));
        }

        if (catalogIndexProjectionContext.Operation is 6 or 7)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Text);
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.FullTextOptions);
        }

        if (catalogIndexProjectionContext.Operation is 17 or 18)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.TableDefinition);
        }

        if (catalogIndexProjectionContext.Operation == 8)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.SpecializedOptions);
        }
    }
}
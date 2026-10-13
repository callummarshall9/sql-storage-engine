// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexDocuments;
internal sealed partial class CatalogIndexDocumentProjectionOrchestrationService
{
    private static void ValidateCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext);

        if (catalogIndexProjectionContext.Operation is (< 1 or > 5) and not 16)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogIndexProjectionContext));
        }

        if (catalogIndexProjectionContext.Operation == 1)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Document);
        }

        if (catalogIndexProjectionContext.Operation is 2 or 4 or 16)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Text);
        }

        if (catalogIndexProjectionContext.Operation is 4 or 16)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Collation);
        }

        if (catalogIndexProjectionContext.Operation == 5)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Encryption);
            ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext.Bytes);
        }
    }
}
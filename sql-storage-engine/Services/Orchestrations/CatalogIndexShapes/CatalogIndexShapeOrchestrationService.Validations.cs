// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
internal sealed partial class CatalogIndexShapeOrchestrationService
{
    private static void ValidateCatalogIndex(CatalogIndex catalogIndex)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndex);

        if (catalogIndex.ColumnSource is null && catalogIndex.Columns is null)
        {
            throw new ArgumentNullException(paramName: "columns");
        }
    }
}
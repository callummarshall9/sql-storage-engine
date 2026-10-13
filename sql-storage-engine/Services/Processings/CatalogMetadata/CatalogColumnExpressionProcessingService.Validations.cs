// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogColumnExpressionProcessingService
{
    private static void ValidateCatalogColumnExpressionContext(CatalogColumnExpressionContext catalogColumnExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumnExpressionContext);

        if (catalogColumnExpressionContext.Operation is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogColumnExpressionContext));
        }

        if (catalogColumnExpressionContext.Operation is 1 or 3)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogColumnExpressionContext.Expression);
        }

        if (catalogColumnExpressionContext.Operation == 1)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogColumnExpressionContext.ColumnName);
        }

        if (catalogColumnExpressionContext.Operation is 2 or 3)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogColumnExpressionContext.Columns);
        }
    }
}
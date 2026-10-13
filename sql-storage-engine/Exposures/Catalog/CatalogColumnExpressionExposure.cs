// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogColumnExpressionExposure
{
    private static readonly ICatalogColumnExpressionProcessingService service = new CatalogColumnExpressionProcessingService();
    public static bool ExpressionReferencesColumn(string expression, string columnName) =>
        service.PrepareCatalogColumnExpressionContext(catalogColumnExpressionContext: new CatalogColumnExpressionContext { Expression = expression, ColumnName = columnName, Operation = 1 })
            .ReferencesColumn;

    public static IReadOnlyList<int> GetComputedColumnOrder(IReadOnlyList<CatalogColumn> columns) =>
        service.PrepareCatalogColumnExpressionContext(catalogColumnExpressionContext: new CatalogColumnExpressionContext { Columns = columns, Operation = 2 })
            .ComputedColumnOrder!;

    public static CatalogColumn? ResolveDirectColumnReference(string expression, IReadOnlyList<CatalogColumn> columns) =>
        service.PrepareCatalogColumnExpressionContext(catalogColumnExpressionContext: new CatalogColumnExpressionContext { Expression = expression, Columns = columns, Operation = 3 })
            .DirectColumnReference;
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogIndexedColumnExposure
{
    private static readonly ICatalogIndexedColumnProcessingService service = new CatalogIndexedColumnProcessingService();
    public static CatalogIndexedColumn CreateCatalogIndexedColumn(ColumnId columnId, SortDirection direction, NullSortOrder nullSortOrder, string? collation = null) =>
        service.PrepareCatalogIndexedColumn(catalogIndexedColumn: new CatalogIndexedColumn { ColumnId = columnId, Direction = direction, NullSortOrder = nullSortOrder, Collation = collation });

    public static CatalogIndexedColumn PrepareCatalogIndexedColumn(CatalogIndexedColumn catalogIndexedColumn) =>
        service.PrepareCatalogIndexedColumn(catalogIndexedColumn: catalogIndexedColumn);
}
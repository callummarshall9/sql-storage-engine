// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogSystemVersioningExposure
{
    private static readonly ICatalogSystemVersioningProcessingService service = new CatalogSystemVersioningProcessingService();
    public static CatalogSystemVersioning CreateCatalogSystemVersioning(TableId tableId, ulong periodStartColumnId, ulong periodEndColumnId) =>
        service.PrepareCatalogSystemVersioning(catalogSystemVersioning: new CatalogSystemVersioning { HistoryTableId = tableId, PeriodStartColumnId = new ColumnId { Value = periodStartColumnId }, PeriodEndColumnId = new ColumnId { Value = periodEndColumnId } });
}
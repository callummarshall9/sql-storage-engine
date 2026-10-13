// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.CatalogMetadata;
using sql_storage_engine.Services.Processings.CatalogIndexShapes;

namespace sql_storage_engine.Services.Orchestrations.CatalogIndexShapes;
internal sealed partial class CatalogIndexShapeOrchestrationService(ICatalogIndexedColumnProcessingService columns, ICatalogIndexShapeProcessingService shapes) : ICatalogIndexShapeOrchestrationService
{
    public CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndex(catalogIndex: catalogIndex);

        var snapshot = (catalogIndex.ColumnSource ?? catalogIndex.Columns).Select(selector: column => columns.PrepareCatalogIndexedColumn(catalogIndexedColumn: column))
            .ToArray();

        var included = (catalogIndex.IncludedColumnSource ?? catalogIndex.IncludedColumns ?? Array.Empty<ColumnId>()).ToArray();
        return shapes.PrepareCatalogIndex(catalogIndex: catalogIndex with { Columns = Array.AsReadOnly(array: snapshot), IncludedColumns = Array.AsReadOnly(array: included) });
    });
}
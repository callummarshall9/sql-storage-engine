// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogIndexShapes;
internal sealed partial class CatalogIndexShapeProcessingService : ICatalogIndexShapeProcessingService
{
    public CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogIndex(catalogIndex: catalogIndex);

        return catalogIndex with
        {
            Columns = Array.AsReadOnly(array: catalogIndex.Columns.ToArray()),
            IncludedColumns = Array.AsReadOnly(array: catalogIndex.IncludedColumns.ToArray()),
            Method = catalogIndex.SpecializedOptions?.Method ?? CatalogIndexMethod.BTree,
            ColumnSource = null,
            IncludedColumnSource = null
        };
    });
}
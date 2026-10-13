// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogBTreeIndexOptionsProcessingService : ICatalogBTreeIndexOptionsProcessingService
{
    public CatalogBTreeIndexOptions PrepareCatalogBTreeIndexOptions(CatalogBTreeIndexOptions catalogBTreeIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogBTreeIndexOptions(catalogBTreeIndexOptions: catalogBTreeIndexOptions);

        return catalogBTreeIndexOptions with
        {
            IncludedColumns = Array.AsReadOnly(array: (catalogBTreeIndexOptions.IncludedColumnSource ?? catalogBTreeIndexOptions.IncludedColumns ?? Array.Empty<ColumnId>()).ToArray()),
            IncludedColumnSource = null
        };
    });
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogBTreeIndexOptionsProcessingService
{
    private static void ValidateCatalogBTreeIndexOptions(CatalogBTreeIndexOptions catalogBTreeIndexOptions)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogBTreeIndexOptions);
    }
}
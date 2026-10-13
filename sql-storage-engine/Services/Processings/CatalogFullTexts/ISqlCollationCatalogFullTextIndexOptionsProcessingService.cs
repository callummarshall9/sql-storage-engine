// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogFullTexts;
internal interface ISqlCollationCatalogFullTextIndexOptionsProcessingService
{
    CatalogFullTextIndexOptions PrepareCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions);

    IReadOnlyList<string> EncodeCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions);

    CatalogFullTextIndexOptions DecodeCatalogFullTextIndexOptions(IReadOnlyList<string> values);
}
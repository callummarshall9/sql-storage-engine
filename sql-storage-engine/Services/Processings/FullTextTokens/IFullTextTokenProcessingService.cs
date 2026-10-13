// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.FullTextTokens;
internal interface IFullTextTokenProcessingService
{
    IReadOnlyList<string> TokenizeCatalogFullTextIndexOptionsDocument(string document, CatalogFullTextIndexOptions catalogFullTextIndexOptions);

    string ParseCatalogFullTextIndexOptionsExactTerm(string term, CatalogFullTextIndexOptions catalogFullTextIndexOptions);
}
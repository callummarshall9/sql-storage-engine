// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Services.Processings.FullTextTokens;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogFullTextTokenExposure
{
    private static readonly IFullTextTokenProcessingService service = new FullTextTokenProcessingService();
    public static IReadOnlyList<string> TokenizeCatalogFullTextIndexOptionsDocument(string document, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        service.TokenizeCatalogFullTextIndexOptionsDocument(document: document, catalogFullTextIndexOptions: catalogFullTextIndexOptions);

    public static string ParseCatalogFullTextIndexOptionsExactTerm(string term, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        service.ParseCatalogFullTextIndexOptionsExactTerm(term: term, catalogFullTextIndexOptions: catalogFullTextIndexOptions);
}
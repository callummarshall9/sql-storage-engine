// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogFullTexts;
using sql_storage_engine.Services.Foundations.SqlCollations;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogFullTextIndexOptionsExposure
{
    private static readonly ISqlCollationCatalogFullTextIndexOptionsProcessingService service = new SqlCollationCatalogFullTextIndexOptionsProcessingService(collations: new SqlCollationService());
    public static CatalogFullTextIndexOptions CreateCatalogFullTextIndexOptions(string collation, string language = CatalogFullTextIndexOptions.UndefinedLanguage, string tokenizer = CatalogFullTextIndexOptions.UnicodeWordTokenizer, ushort tokenizerVersion = CatalogFullTextIndexOptions.UnicodeWordTokenizerVersion, string stoplist = CatalogFullTextIndexOptions.NoStoplist, ushort stoplistVersion = CatalogFullTextIndexOptions.NoStoplistVersion, CatalogFullTextConsistency consistency = CatalogFullTextConsistency.Transactional, int maximumTokensPerDocument = CatalogFullTextIndexOptions.DefaultMaximumTokensPerDocument, int maximumTokenLength = CatalogFullTextIndexOptions.DefaultMaximumTokenLength) =>
        service.PrepareCatalogFullTextIndexOptions(catalogFullTextIndexOptions: new CatalogFullTextIndexOptions { Collation = collation, Language = language, Tokenizer = tokenizer, TokenizerVersion = tokenizerVersion, Stoplist = stoplist, StoplistVersion = stoplistVersion, Consistency = consistency, MaximumTokensPerDocument = maximumTokensPerDocument, MaximumTokenLength = maximumTokenLength });

    public static IReadOnlyList<string> EncodeCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        service.EncodeCatalogFullTextIndexOptions(catalogFullTextIndexOptions: catalogFullTextIndexOptions);

    public static CatalogFullTextIndexOptions DecodeCatalogFullTextIndexOptions(IReadOnlyList<string> values) =>
        service.DecodeCatalogFullTextIndexOptions(values: values);
}
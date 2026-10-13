// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogFullTexts;
internal sealed partial class SqlCollationCatalogFullTextIndexOptionsProcessingService
{
    private void ValidateCatalogFullTextIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogFullTextIndexOptions);
        var collation = catalogFullTextIndexOptions.Collation;
        var language = catalogFullTextIndexOptions.Language;
        var tokenizer = catalogFullTextIndexOptions.Tokenizer;
        var tokenizerVersion = catalogFullTextIndexOptions.TokenizerVersion;
        var stoplist = catalogFullTextIndexOptions.Stoplist;
        var stoplistVersion = catalogFullTextIndexOptions.StoplistVersion;
        var consistency = catalogFullTextIndexOptions.Consistency;
        var maximumTokensPerDocument = catalogFullTextIndexOptions.MaximumTokensPerDocument;
        var maximumTokenLength = catalogFullTextIndexOptions.MaximumTokenLength;
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: collation);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: language);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: tokenizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: stoplist);
        _ = collations.ParseSqlCollation(name: collation);

        if (!StringComparer.Ordinal.Equals(x: language, y: CatalogFullTextIndexOptions.UndefinedLanguage))
        {
            throw new ArgumentException($"Only the language identity '{CatalogFullTextIndexOptions.UndefinedLanguage}' is supported.", nameof(language));
        }

        if (!StringComparer.Ordinal.Equals(x: tokenizer, y: CatalogFullTextIndexOptions.UnicodeWordTokenizer) || tokenizerVersion != CatalogFullTextIndexOptions.UnicodeWordTokenizerVersion)
        {
            throw new ArgumentException($"Only tokenizer '{CatalogFullTextIndexOptions.UnicodeWordTokenizer}' version {CatalogFullTextIndexOptions.UnicodeWordTokenizerVersion} is supported.", nameof(tokenizer));
        }

        if (!StringComparer.Ordinal.Equals(x: stoplist, y: CatalogFullTextIndexOptions.NoStoplist) || stoplistVersion != CatalogFullTextIndexOptions.NoStoplistVersion)
        {
            throw new ArgumentException("Only the versioned no-stoplist identity is supported.", nameof(stoplist));
        }

        if (consistency != CatalogFullTextConsistency.Transactional)
        {
            throw new ArgumentOutOfRangeException(nameof(consistency));
        }

        if (maximumTokensPerDocument is < 1 or > CatalogFullTextIndexOptions.MaximumSupportedTokensPerDocument)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTokensPerDocument));
        }

        if (maximumTokenLength is < 1 or > CatalogFullTextIndexOptions.MaximumSupportedTokenLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTokenLength));
        }
    }

    private static void ValidateCatalogFullTextFields(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);

        if (values.Count != 9)
        {
            throw new ArgumentException(message: "Full-text index metadata must contain exactly nine fields.", paramName: nameof(values));
        }
    }
}
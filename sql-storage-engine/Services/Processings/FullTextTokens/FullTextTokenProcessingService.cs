// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.FullTextTokens;
/// <summary>Applies the version-one culture-neutral Unicode tokenization and quota contract.</summary>
internal sealed partial class FullTextTokenProcessingService : IFullTextTokenProcessingService
{
    public IReadOnlyList<string> TokenizeCatalogFullTextIndexOptionsDocument(string document, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateDocument(document: document, catalogFullTextIndexOptions: catalogFullTextIndexOptions);
        var tokens = Tokenize(value: document, maximumTokenLength: catalogFullTextIndexOptions.MaximumTokenLength);

        if (tokens.Count > catalogFullTextIndexOptions.MaximumTokensPerDocument)
        {
            throw new StorageResourceExhaustedException(message: $"Full-text document exceeds the {catalogFullTextIndexOptions.MaximumTokensPerDocument} token limit.");
        }

        return tokens;
    });

    public string ParseCatalogFullTextIndexOptionsExactTerm(string term, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateTerm(term: term, catalogFullTextIndexOptions: catalogFullTextIndexOptions);

        var normalized = term.Trim()
            .Normalize(normalizationForm: NormalizationForm.FormC);

        var tokens = Tokenize(value: normalized, maximumTokenLength: catalogFullTextIndexOptions.MaximumTokenLength);

        if (tokens.Count != 1 || !StringComparer.Ordinal.Equals(x: tokens[0], y: normalized))
        {
            throw new ArgumentException(message: "A full-text term query must contain exactly one Unicode word token.", paramName: nameof(term));
        }

        return tokens[0];
    });

    private static IReadOnlyList<string> Tokenize(string value, int maximumTokenLength)
    {
        value = value.Normalize(normalizationForm: NormalizationForm.FormC);
        List<string> tokens = [];
        var token = new StringBuilder();

        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(value: rune);
            var word = category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.ConnectorPunctuation;
            var combining = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

            if (word || combining && token.Length != 0)
            {
                token.Append(value: rune);

                if (token.Length > maximumTokenLength)
                {
                    throw new StorageResourceExhaustedException($"Full-text token exceeds the {maximumTokenLength} UTF-16 code-unit limit.");
                }

                continue;
            }

            Flush(tokens: tokens, token: token);
        }

        Flush(tokens: tokens, token: token);
        return tokens;
    }

    private static void Flush(ICollection<string> tokens, StringBuilder token)
    {
        if (token.Length == 0)
        {
            return;
        }

        tokens.Add(item: token.ToString());
        token.Clear();
    }
}
using System.Globalization;
using System.Text;
using sql_storage_engine.Catalog;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

/// <summary>Implements the version-one, culture-neutral Unicode word boundary contract.</summary>
internal static class FullTextTokenizer
{
    public static IReadOnlyList<string> TokenizeDocument(string document, CatalogFullTextIndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var tokens = Tokenize(document, options.MaximumTokenLength);
        if (tokens.Count > options.MaximumTokensPerDocument)
            throw new StorageResourceExhaustedException(
                $"Full-text document exceeds the {options.MaximumTokensPerDocument} token limit.");
        return tokens;
    }

    public static string ParseExactTerm(string term, CatalogFullTextIndexOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);
        ArgumentNullException.ThrowIfNull(options);
        var normalized = term.Trim().Normalize(NormalizationForm.FormC);
        var tokens = Tokenize(normalized, options.MaximumTokenLength);
        if (tokens.Count != 1 || !StringComparer.Ordinal.Equals(tokens[0], normalized))
            throw new ArgumentException("A full-text term query must contain exactly one Unicode word token.",
                nameof(term));
        return tokens[0];
    }

    private static IReadOnlyList<string> Tokenize(string value, int maximumTokenLength)
    {
        value = value.Normalize(NormalizationForm.FormC);
        List<string> tokens = [];
        var token = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var word = category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
                UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or
                UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or
                UnicodeCategory.ConnectorPunctuation;
            var combining = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark;
            if (word || combining && token.Length != 0)
            {
                token.Append(rune);
                if (token.Length > maximumTokenLength)
                    throw new StorageResourceExhaustedException(
                        $"Full-text token exceeds the {maximumTokenLength} UTF-16 code-unit limit.");
                continue;
            }
            Flush(tokens, token);
        }
        Flush(tokens, token);
        return tokens;
    }

    private static void Flush(ICollection<string> tokens, StringBuilder token)
    {
        if (token.Length == 0) return;
        tokens.Add(token.ToString());
        token.Clear();
    }
}

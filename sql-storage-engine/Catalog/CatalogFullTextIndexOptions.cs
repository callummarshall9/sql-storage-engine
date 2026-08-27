using System.Globalization;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Catalog;

/// <summary>
/// Identifies the complete, durable linguistic and resource contract of a full-text term index.
/// </summary>
public sealed record CatalogFullTextIndexOptions
{
    public const string UnicodeWordTokenizer = "unicode-word";
    public const ushort UnicodeWordTokenizerVersion = 1;
    public const string UndefinedLanguage = "und";
    public const string NoStoplist = "none";
    public const ushort NoStoplistVersion = 0;
    public const int DefaultMaximumTokensPerDocument = 16_384;
    public const int DefaultMaximumTokenLength = 128;
    public const int MaximumSupportedTokensPerDocument = 65_535;
    public const int MaximumSupportedTokenLength = 128;

    public CatalogFullTextIndexOptions(string collation,
        string language = UndefinedLanguage,
        string tokenizer = UnicodeWordTokenizer,
        ushort tokenizerVersion = UnicodeWordTokenizerVersion,
        string stoplist = NoStoplist,
        ushort stoplistVersion = NoStoplistVersion,
        CatalogFullTextConsistency consistency = CatalogFullTextConsistency.Transactional,
        int maximumTokensPerDocument = DefaultMaximumTokensPerDocument,
        int maximumTokenLength = DefaultMaximumTokenLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collation);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(stoplist);
        _ = SqlCollation.Parse(collation);
        if (!StringComparer.Ordinal.Equals(language, UndefinedLanguage))
            throw new ArgumentException($"Only the language identity '{UndefinedLanguage}' is supported.", nameof(language));
        if (!StringComparer.Ordinal.Equals(tokenizer, UnicodeWordTokenizer) ||
            tokenizerVersion != UnicodeWordTokenizerVersion)
            throw new ArgumentException(
                $"Only tokenizer '{UnicodeWordTokenizer}' version {UnicodeWordTokenizerVersion} is supported.",
                nameof(tokenizer));
        if (!StringComparer.Ordinal.Equals(stoplist, NoStoplist) || stoplistVersion != NoStoplistVersion)
            throw new ArgumentException("Only the versioned no-stoplist identity is supported.", nameof(stoplist));
        if (consistency != CatalogFullTextConsistency.Transactional)
            throw new ArgumentOutOfRangeException(nameof(consistency));
        if (maximumTokensPerDocument is < 1 or > MaximumSupportedTokensPerDocument)
            throw new ArgumentOutOfRangeException(nameof(maximumTokensPerDocument));
        if (maximumTokenLength is < 1 or > MaximumSupportedTokenLength)
            throw new ArgumentOutOfRangeException(nameof(maximumTokenLength));

        Collation = collation;
        Language = language;
        Tokenizer = tokenizer;
        TokenizerVersion = tokenizerVersion;
        Stoplist = stoplist;
        StoplistVersion = stoplistVersion;
        Consistency = consistency;
        MaximumTokensPerDocument = maximumTokensPerDocument;
        MaximumTokenLength = maximumTokenLength;
    }

    public string Collation { get; }
    public string Language { get; }
    public string Tokenizer { get; }
    public ushort TokenizerVersion { get; }
    public string Stoplist { get; }
    public ushort StoplistVersion { get; }
    public CatalogFullTextConsistency Consistency { get; }
    public int MaximumTokensPerDocument { get; }
    public int MaximumTokenLength { get; }

    internal IReadOnlyList<string> EncodeMetadata() =>
    [
        Collation,
        Language,
        Tokenizer,
        TokenizerVersion.ToString(CultureInfo.InvariantCulture),
        Stoplist,
        StoplistVersion.ToString(CultureInfo.InvariantCulture),
        ((byte)Consistency).ToString(CultureInfo.InvariantCulture),
        MaximumTokensPerDocument.ToString(CultureInfo.InvariantCulture),
        MaximumTokenLength.ToString(CultureInfo.InvariantCulture)
    ];

    internal static CatalogFullTextIndexOptions DecodeMetadata(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != 9)
            throw new ArgumentException("Full-text index metadata must contain exactly nine fields.", nameof(values));
        try
        {
            return new CatalogFullTextIndexOptions(values[0], values[1], values[2],
                ushort.Parse(values[3], NumberStyles.None, CultureInfo.InvariantCulture),
                values[4], ushort.Parse(values[5], NumberStyles.None, CultureInfo.InvariantCulture),
                (CatalogFullTextConsistency)byte.Parse(values[6], NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(values[7], NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(values[8], NumberStyles.None, CultureInfo.InvariantCulture));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Full-text index metadata contains an invalid numeric field.", nameof(values),
                exception);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("Full-text index metadata contains an out-of-range numeric field.",
                nameof(values), exception);
        }
    }
}

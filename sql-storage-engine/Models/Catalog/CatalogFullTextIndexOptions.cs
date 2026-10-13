// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Models.Catalog;
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
    public string Collation { get => _collation!; init => _collation = value; }
    public string Language { get => _language!; init => _language = value; }
    public string Tokenizer { get => _tokenizer!; init => _tokenizer = value; }

    private string? _collation;
    private string? _language;
    private string? _tokenizer;
    public ushort TokenizerVersion { get; init; }
    public string Stoplist { get => _stoplist!; init => _stoplist = value; }

    private string? _stoplist;
    public ushort StoplistVersion { get; init; }
    public CatalogFullTextConsistency Consistency { get; init; }
    public int MaximumTokensPerDocument { get; init; }
    public int MaximumTokenLength { get; init; }
}
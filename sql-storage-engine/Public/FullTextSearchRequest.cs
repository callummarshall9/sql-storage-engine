using sql_storage_engine.Catalog;

namespace sql_storage_engine;

/// <summary>A single exact term and language identity for a full-text index search.</summary>
public sealed record FullTextSearchRequest
{
    public FullTextSearchRequest(string term, string language = CatalogFullTextIndexOptions.UndefinedLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(term);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Term = term;
        Language = language;
    }

    public string Term { get; }
    public string Language { get; }
}

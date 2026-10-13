// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine;
/// <summary>A single exact term and language identity for a full-text index search.</summary>
public sealed record FullTextSearchRequest
{
    public FullTextSearchRequest(string term, string language = CatalogFullTextIndexOptions.UndefinedLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: term);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: language);
        Term = term;
        Language = language;
    }

    public string Term { get; }
    public string Language { get; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.FullTextTokens;
internal sealed partial class FullTextTokenProcessingService
{
    private static void ValidateDocument(string document, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        Validate(inputs: [(document is null, true, nameof(document)), (catalogFullTextIndexOptions is null, true, nameof(catalogFullTextIndexOptions))]);

    private static void ValidateTerm(string term, CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: term), term is null, nameof(term)), (catalogFullTextIndexOptions is null, true, nameof(catalogFullTextIndexOptions))]);

    private static void Validate((bool Invalid, bool Missing, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "Input must not be blank.", paramName: input.ParameterName);
        }
    }
}
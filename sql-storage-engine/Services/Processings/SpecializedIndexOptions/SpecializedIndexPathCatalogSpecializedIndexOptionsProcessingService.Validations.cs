// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.SpecializedIndexOptions;
internal sealed partial class SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService
{
    private static void ValidateOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogSpecializedIndexOptions);

        if (catalogSpecializedIndexOptions.Method is not (CatalogIndexMethod.Json or CatalogIndexMethod.Xml or CatalogIndexMethod.Vector or CatalogIndexMethod.Spatial or CatalogIndexMethod.FullText))
        {
            throw new ArgumentException(message: "A specialized index method is required.");
        }

        if (catalogSpecializedIndexOptions.Method == CatalogIndexMethod.Vector && (catalogSpecializedIndexOptions.VectorMetric is not { } metric || !Enum.IsDefined(value: metric)))
        {
            throw new ArgumentOutOfRangeException(paramName: "metric");
        }

        if (catalogSpecializedIndexOptions.Method == CatalogIndexMethod.FullText)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogSpecializedIndexOptions.FullTextOptions);
        }

        if (catalogSpecializedIndexOptions.Method == CatalogIndexMethod.Xml && (catalogSpecializedIndexOptions.XmlIndexKind is not { } kind || !Enum.IsDefined(value: kind)))
        {
            throw new ArgumentOutOfRangeException(paramName: "kind");
        }

        if (catalogSpecializedIndexOptions.RequirePaths)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogSpecializedIndexOptions.PathSource);
        }

        if (catalogSpecializedIndexOptions.RequireNamespaces)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogSpecializedIndexOptions.NamespaceSource);
        }
    }

    private static void ValidateUnique(string[] values)
    {
        if (values.Distinct(comparer: StringComparer.Ordinal)
            .Count() != values.Length)
        {
            throw new ArgumentException(message: "Index paths must be unique.", paramName: "paths");
        }
    }

    private static void ValidateJsonPaths(string[] values)
    {
        if (values.Any(predicate: value => string.IsNullOrWhiteSpace(value: value) || !value.StartsWith(value: '$')))
        {
            throw new ArgumentException(message: "A JSON index requires absolute JSON paths.", paramName: "paths");
        }

        ValidateUnique(values: values);

        for (var left = 0; left < values.Length; left++)
        {
            for (var right = left + 1; right < values.Length; right++)
            {
                if (PathsOverlap(left: values[left], right: values[right]))
                {
                    throw new ArgumentException(message: "JSON index paths cannot overlap.", paramName: "paths");
                }
            }
        }
    }

    private static void ValidateXmlPaths(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions, string[] values)
    {
        if (catalogSpecializedIndexOptions.XmlIndexKind == CatalogXmlIndexKind.Selective && (values.Length == 0 || values.Length > 1024))
        {
            throw new ArgumentException(message: "A selective XML index requires between 1 and 1024 paths.", paramName: "paths");
        }

        if (values.Any(predicate: value => string.IsNullOrWhiteSpace(value: value) || !value.StartsWith(value: '/')))
        {
            throw new ArgumentException(message: "XML index paths must be non-empty and absolute.", paramName: "paths");
        }

        ValidateUnique(values: values);
    }

    private static void ValidateNamespace(string prefix, string uri)
    {
        if (string.IsNullOrWhiteSpace(value: prefix) || prefix.Equals(value: "xmlns", comparisonType: StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(value: uri))
        {
            throw new ArgumentException(message: "XML namespace prefixes and URIs are required.", paramName: "namespaces");
        }
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogSpecializedIndexOptions
{
    public CatalogIndexMethod Method { get; init; }
    public IReadOnlyList<string> JsonPaths { get => jsonPaths!; init => jsonPaths = value; }

    private IReadOnlyList<string>? jsonPaths;
    public SqlVectorDistanceMetric? VectorMetric { get; init; }
    public CatalogXmlIndexKind? XmlIndexKind { get; init; }
    public IReadOnlyDictionary<string, string> XmlNamespaces { get => xmlNamespaces!; init => xmlNamespaces = value; }

    private IReadOnlyDictionary<string, string>? xmlNamespaces;
    public int? SpatialSrid { get; init; }
    public CatalogFullTextIndexOptions? FullTextOptions { get; init; }
    internal IEnumerable<string>? PathSource { get; init; }
    internal IReadOnlyDictionary<string, string>? NamespaceSource { get; init; }
    internal bool RequirePaths { get; init; }
    internal bool RequireNamespaces { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Graph;
public sealed record GraphTraversalOptions
{
    public const int MaximumSupportedEdges = 100_000;
    public int MaximumEdges { get; init; }
}
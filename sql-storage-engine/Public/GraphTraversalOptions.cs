namespace sql_storage_engine;

/// <summary>Invocation-owned limits for one adjacency lookup.</summary>
public sealed record GraphTraversalOptions
{
    public const int MaximumSupportedEdges = 100_000;

    public GraphTraversalOptions(int maximumEdges = 10_000)
    {
        if (maximumEdges is <= 0 or > MaximumSupportedEdges)
            throw new ArgumentOutOfRangeException(nameof(maximumEdges),
                $"A graph traversal limit must be from 1 through {MaximumSupportedEdges} edges.");
        MaximumEdges = maximumEdges;
    }

    public int MaximumEdges { get; }
}

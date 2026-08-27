using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

/// <summary>A full-text match. Exact-term predicate searches deliberately expose no ranking value.</summary>
public sealed record FullTextIndexMatch(RowId RowId)
{
    /// <summary>Ranking is unavailable for the exact-term predicate capability.</summary>
    public double? Rank => null;
}

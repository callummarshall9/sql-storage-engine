// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
/// <summary>A full-text match. Exact-term predicate searches deliberately expose no ranking value.</summary>
public sealed record FullTextIndexMatch
{
    public RowId RowId { get; init; }
    /// <summary>Ranking is unavailable for the exact-term predicate capability.</summary>
    public double? Rank { get; init; }
}
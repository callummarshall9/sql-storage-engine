// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models;
public sealed record SpecializedIndexMatch
{
    public RowId RowId { get; init; }
    public double Distance { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Tables;
public sealed record TableUpdateResult
{
    public bool Updated { get; init; }
    public RowId PreviousRowId { get; init; }
    public RowId CurrentRowId { get; init; }
    public bool Relocated { get; init; }
}
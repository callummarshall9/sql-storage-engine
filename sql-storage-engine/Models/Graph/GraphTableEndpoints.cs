// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Graph;
public sealed record GraphTableEndpoints
{
    public TableId FromNodeTableId { get; init; }
    public TableId ToNodeTableId { get; init; }
}
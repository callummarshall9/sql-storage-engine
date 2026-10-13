// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Models;
/// <summary>A graph node identity and its user-visible payload.</summary>
public sealed record StoredGraphNode
{
    public GraphNodeId NodeId { get; init; }
    public Row Row { get => _row!; init => _row = value; }

    private Row? _row;
}
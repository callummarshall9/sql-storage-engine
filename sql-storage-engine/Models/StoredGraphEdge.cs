// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Models;
/// <summary>A graph edge identity, endpoints, and user-visible payload.</summary>
public sealed record StoredGraphEdge
{
    public GraphEdgeId EdgeId { get; init; }
    public GraphNodeId FromNodeId { get; init; }
    public GraphNodeId ToNodeId { get; init; }
    public Row Row { get => _row!; init => _row = value; }

    private Row? _row;
}
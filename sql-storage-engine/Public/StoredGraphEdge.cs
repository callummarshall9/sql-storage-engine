using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>A graph edge identity, endpoints, and user-visible payload.</summary>
public sealed record StoredGraphEdge(GraphEdgeId EdgeId, GraphNodeId FromNodeId, GraphNodeId ToNodeId, Row Row);

using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>A graph node identity and its user-visible payload.</summary>
public sealed record StoredGraphNode(GraphNodeId NodeId, Row Row);

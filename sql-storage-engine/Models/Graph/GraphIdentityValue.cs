// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Graph;
internal sealed record GraphIdentityValue
{
    public GraphIdentity? Identity { get; set; }
    public SqlValue? Value { get; set; }
    public GraphNodeId NodeId { get; set; }
    public GraphEdgeId EdgeId { get; set; }
    public string? Text { get; set; }
}
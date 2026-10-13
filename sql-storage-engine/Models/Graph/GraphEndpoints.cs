// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Graph;
public sealed record GraphEndpoints
{
    public GraphNodeId FromNodeId { get; init; }
    public GraphNodeId ToNodeId { get; init; }
}
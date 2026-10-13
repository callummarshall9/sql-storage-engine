// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Graph;
public readonly record struct GraphEdgeId
{
    public DatabaseId DatabaseId { get; init; }
    public TableId TableId { get; init; }
    public ulong SchemaVersion { get; init; }
    public Guid Value { get; init; }
}
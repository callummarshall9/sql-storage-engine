// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record SqlSpatial
{
    public SqlSpatialKind Kind { get; init; }
    public int Srid { get; init; }
    public string WellKnownText { get => wellKnownText!; init => wellKnownText = value; }

    private string? wellKnownText;
    internal SqlSpatialShape? Shape { get; init; }
    internal string? ShapeContent { get; init; }
}
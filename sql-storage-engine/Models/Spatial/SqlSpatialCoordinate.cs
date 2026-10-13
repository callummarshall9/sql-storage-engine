// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Spatial;
internal readonly record struct SqlSpatialCoordinate
{
    public double X { get; init; }
    public double Y { get; init; }
    public double? Z { get; init; }
    public double? M { get; init; }
}
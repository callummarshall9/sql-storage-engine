// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Spatial;
internal readonly record struct SqlSpatialEnvelope
{
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
}
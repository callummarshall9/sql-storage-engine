// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Rows;
internal static class SqlSpatialShapeTypeExtensions
{
    public static string ToSqlName(this SqlSpatialShapeType type) =>
        type switch
    {
        SqlSpatialShapeType.LineString => "LineString",
        SqlSpatialShapeType.MultiPoint => "MultiPoint",
        SqlSpatialShapeType.MultiLineString => "MultiLineString",
        SqlSpatialShapeType.MultiPolygon => "MultiPolygon",
        SqlSpatialShapeType.GeometryCollection => "GeometryCollection",
        SqlSpatialShapeType.CircularString => "CircularString",
        SqlSpatialShapeType.CompoundCurve => "CompoundCurve",
        SqlSpatialShapeType.CurvePolygon => "CurvePolygon",
        SqlSpatialShapeType.FullGlobe => "FullGlobe",
        _ => type.ToString()};
}
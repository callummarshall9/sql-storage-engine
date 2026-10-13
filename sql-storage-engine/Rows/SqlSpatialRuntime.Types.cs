// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Rows;
internal enum SqlSpatialShapeType : byte
{
    Point = 1,
    LineString,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    GeometryCollection,
    CircularString,
    CompoundCurve,
    CurvePolygon,
    FullGlobe
}
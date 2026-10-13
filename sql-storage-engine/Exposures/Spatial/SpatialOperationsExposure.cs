// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Services.Processings.SpatialOperations;

namespace sql_storage_engine.Exposures.Spatial;
internal static class SpatialOperationsExposure
{
    private static readonly ISqlSpatialShapeOperationsProcessingService service = new SqlSpatialShapeOperationsProcessingService();
    public static void CheckSqlSpatialShapeGeography(SqlSpatialShape sqlSpatialShape) =>
        service.CheckSqlSpatialShapeGeography(sqlSpatialShape: sqlSpatialShape);

    public static bool CheckValidSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        service.CheckValidSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

    public static (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialShapeBounds(SqlSpatialShape sqlSpatialShape) =>
        service.ReadSqlSpatialShapeBounds(sqlSpatialShape: sqlSpatialShape);

    public static SqlSpatialShape CreateSqlSpatialShapeEnvelope(SqlSpatialShape sqlSpatialShape) =>
        service.CreateSqlSpatialShapeEnvelope(sqlSpatialShape: sqlSpatialShape);

    public static double MeasureSqlSpatialShapeLength(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind) =>
        service.MeasureSqlSpatialShapeLength(sqlSpatialShape: sqlSpatialShape, kind: kind);

    public static double MeasureSqlSpatialShapeArea(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind) =>
        service.MeasureSqlSpatialShapeArea(sqlSpatialShape: sqlSpatialShape, kind: kind);

    public static bool CheckEquivalentSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        service.CheckEquivalentSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);

    public static bool CheckIntersectingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        service.CheckIntersectingSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);

    public static bool CheckContainingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        service.CheckContainingSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);

    public static double MeasureSqlSpatialShapeDistance(SqlSpatialShape[] sqlSpatialShapes, SqlSpatialKind kind) =>
        service.MeasureSqlSpatialShapeDistance(sqlSpatialShapes: sqlSpatialShapes, kind: kind);
}
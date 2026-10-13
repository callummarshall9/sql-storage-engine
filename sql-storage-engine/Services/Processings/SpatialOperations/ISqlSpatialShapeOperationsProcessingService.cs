// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialOperations;
internal interface ISqlSpatialShapeOperationsProcessingService
{
    void CheckSqlSpatialShapeGeography(SqlSpatialShape sqlSpatialShape);

    bool CheckValidSqlSpatialShape(SqlSpatialShape sqlSpatialShape);

    (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialShapeBounds(SqlSpatialShape sqlSpatialShape);

    SqlSpatialShape CreateSqlSpatialShapeEnvelope(SqlSpatialShape sqlSpatialShape);

    double MeasureSqlSpatialShapeLength(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind);

    double MeasureSqlSpatialShapeArea(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind);

    bool CheckEquivalentSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes);

    bool CheckIntersectingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes);

    bool CheckContainingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes);

    double MeasureSqlSpatialShapeDistance(SqlSpatialShape[] sqlSpatialShapes, SqlSpatialKind kind);
}
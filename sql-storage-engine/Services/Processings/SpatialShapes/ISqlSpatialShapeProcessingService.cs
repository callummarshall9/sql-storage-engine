// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialShapes;
internal interface ISqlSpatialShapeProcessingService
{
    SqlSpatialShape PrepareSqlSpatialShape(SqlSpatialShape sqlSpatialShape);

    string FormatSqlSpatialShape(SqlSpatialShape sqlSpatialShape);

    string FormatSqlSpatialShapeCoordinate(SqlSpatialShape sqlSpatialShape);

    int ReadSqlSpatialShapeDimension(SqlSpatialShape sqlSpatialShape);

    SqlSpatialShape FlattenSqlSpatialShape(SqlSpatialShape sqlSpatialShape);

    string FormatSqlSpatialShapeGml(SqlSpatialShape sqlSpatialShape, int srid);

    string WriteSqlSpatialShapeContent(SqlSpatialShape sqlSpatialShape);

    SqlSpatialShape ReadSqlSpatialShapeContent(string content);

    (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialShapeBounds(SqlSpatialShape sqlSpatialShape);
}
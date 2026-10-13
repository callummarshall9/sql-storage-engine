// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Services.Processings.SpatialShapes;

namespace sql_storage_engine.Exposures.Spatial;
internal static class SpatialShapeExposure
{
    private static readonly ISqlSpatialShapeProcessingService service = new SqlSpatialShapeProcessingService();
    public static SqlSpatialShape PrepareSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        service.PrepareSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

    public static string FormatSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        service.FormatSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

    public static string FormatSqlSpatialCoordinate(SqlSpatialCoordinate sqlSpatialCoordinate) =>
        service.FormatSqlSpatialShapeCoordinate(sqlSpatialShape: new SqlSpatialShape { Type = SqlSpatialShapeType.Point, Points = [sqlSpatialCoordinate], Children = Array.Empty<SqlSpatialShape>() });

    public static SqlSpatialShape CreateSqlSpatialShapePoint(SqlSpatialCoordinate sqlSpatialCoordinate) =>
        service.PrepareSqlSpatialShape(sqlSpatialShape: new SqlSpatialShape { Type = SqlSpatialShapeType.Point, PointSource = [sqlSpatialCoordinate] });

    public static int ReadSqlSpatialShapeDimension(SqlSpatialShape sqlSpatialShape) =>
        service.ReadSqlSpatialShapeDimension(sqlSpatialShape: sqlSpatialShape);

    public static IEnumerable<SqlSpatialCoordinate> ReadSqlSpatialShapeCoordinates(SqlSpatialShape sqlSpatialShape) =>
        service.FlattenSqlSpatialShape(sqlSpatialShape: sqlSpatialShape)
            .Points;

    public static string FormatSqlSpatialShapeGml(SqlSpatialShape sqlSpatialShape, int srid) =>
        service.FormatSqlSpatialShapeGml(sqlSpatialShape: sqlSpatialShape, srid: srid);
}
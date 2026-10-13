// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialShapes;
internal sealed partial class SqlSpatialShapeProcessingService
{
    private static void ValidateSqlSpatialShapeInput(SqlSpatialShape sqlSpatialShape) =>
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);

    private static void ValidateSqlSpatialShape(SqlSpatialShape sqlSpatialShape)
    {
        ValidateSqlSpatialShapeInput(sqlSpatialShape: sqlSpatialShape);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Points);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Children);
    }

    private static void ValidateSqlSpatialShapeGml(SqlSpatialShape sqlSpatialShape, int srid)
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
    }

    private static void ValidateSqlSpatialShapeContentSource(string content)
    {
        ArgumentNullException.ThrowIfNull(argument: content);
    }
}
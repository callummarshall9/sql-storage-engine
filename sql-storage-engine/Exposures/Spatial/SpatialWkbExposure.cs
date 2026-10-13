// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Services.Processings.SpatialWkbs;

namespace sql_storage_engine.Exposures.Spatial;
internal static class SpatialWkbExposure
{
    private static readonly ISqlSpatialShapeWkbProcessingService service = new SqlSpatialShapeWkbProcessingService();
    public static byte[] EncodeSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape) =>
        service.EncodeSqlSpatialShapeWkb(sqlSpatialShape: sqlSpatialShape);

    public static SqlSpatialShape DecodeSqlSpatialShapeWkb(ReadOnlySpan<byte> value) =>
        service.DecodeSqlSpatialShapeWkb(value: value);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Services.Foundations.SpatialBinaries;

namespace sql_storage_engine.Exposures.Spatial;
internal static class SpatialBinaryExposure
{
    private static readonly ISqlSpatialShapeBinaryService service = new SqlSpatialShapeBinaryService();
    public static byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape) =>
        service.EncodeSqlSpatialShape(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);

    public static (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(ReadOnlySpan<byte> value) =>
        service.DecodeSqlSpatialShape(value: value);
}
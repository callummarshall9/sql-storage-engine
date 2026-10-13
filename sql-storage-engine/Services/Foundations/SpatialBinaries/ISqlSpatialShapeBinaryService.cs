// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Foundations.SpatialBinaries;
internal interface ISqlSpatialShapeBinaryService
{
    byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape);

    (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(ReadOnlySpan<byte> value);
}
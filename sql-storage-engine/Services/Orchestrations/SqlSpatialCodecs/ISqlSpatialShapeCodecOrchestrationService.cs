// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlSpatialCodecs;
internal interface ISqlSpatialShapeCodecOrchestrationService
{
    byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape);

    byte[] EncodeSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape);

    (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(byte[] value);

    SqlSpatialShape DecodeSqlSpatialShapeWkb(byte[] value);
}
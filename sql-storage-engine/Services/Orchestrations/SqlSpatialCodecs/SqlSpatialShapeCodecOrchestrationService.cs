// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Processings.SpatialWkbs;
using sql_storage_engine.Services.Processings.SpatialBinaries;

namespace sql_storage_engine.Services.Orchestrations.SqlSpatialCodecs;
internal sealed partial class SqlSpatialShapeCodecOrchestrationService : ISqlSpatialShapeCodecOrchestrationService
{
    private readonly ISqlSpatialShapeWkbProcessingService wkb;
    private readonly ISqlSpatialShapeBinaryProcessingService binary;
    public SqlSpatialShapeCodecOrchestrationService(ISqlSpatialShapeWkbProcessingService wkb, ISqlSpatialShapeBinaryProcessingService binary)
    {
        this.wkb = wkb;
        this.binary = binary;
    }

    public byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape) =>
        TryCatch<byte[]>(operation: () =>
    {
        ValidateShape(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);
        return binary.EncodeSqlSpatialShape(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);
    });

    public byte[] EncodeSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape) =>
        TryCatch<byte[]>(operation: () =>
    {
        ValidateShape(sqlSpatialShape: sqlSpatialShape);
        return wkb.EncodeSqlSpatialShapeWkb(sqlSpatialShape: sqlSpatialShape);
    });

    public (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(byte[] value) =>
        TryCatch<(SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape)>(operation: () =>
    {
        ValidateBytes(value: value);
        return binary.DecodeSqlSpatialShape(value: value);
    });

    public SqlSpatialShape DecodeSqlSpatialShapeWkb(byte[] value) =>
        TryCatch<SqlSpatialShape>(operation: () =>
    {
        ValidateBytes(value: value);
        return wkb.DecodeSqlSpatialShapeWkb(value: value);
    });
}
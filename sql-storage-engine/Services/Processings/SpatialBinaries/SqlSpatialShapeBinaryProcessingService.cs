// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialBinaries;
using sql_storage_engine.Services.Foundations.SpatialBinaries;

internal sealed partial class SqlSpatialShapeBinaryProcessingService : ISqlSpatialShapeBinaryProcessingService
{
    private readonly SqlSpatialShapeBinaryService binary;
    public SqlSpatialShapeBinaryProcessingService(SqlSpatialShapeBinaryService binary)
    {
        this.binary = binary;
    }

    public byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeSource(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);
        return binary.EncodeSqlSpatialShape(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);
    });

    public (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(ReadOnlySpan<byte> value) =>
        TryCatch(operation: context =>
    {
        ValidateSqlSpatialShapeBytes(value: context.value);
        return binary.DecodeSqlSpatialShape(value: context.value);
    }, context: new ReadableSqlSpatialBinaryContext { value = value });

    internal static SqlSpatialShapeBinaryProcessingService CreateSqlSpatialShapeBinaryProcessingService() =>
        new SqlSpatialShapeBinaryProcessingService(binary: new sql_storage_engine.Services.Foundations.SpatialBinaries.SqlSpatialShapeBinaryService());
}
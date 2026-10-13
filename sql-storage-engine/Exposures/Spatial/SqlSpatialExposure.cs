// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Services.Coordinations.SqlSpatials;
using sql_storage_engine.Services.Orchestrations.SqlSpatials;
using sql_storage_engine.Services.Orchestrations.SqlSpatialCodecs;
using sql_storage_engine.Services.Processings.SpatialWkts;
using sql_storage_engine.Services.Processings.SpatialShapes;
using sql_storage_engine.Services.Processings.SpatialOperations;
using sql_storage_engine.Services.Processings.SpatialWkbs;
using sql_storage_engine.Services.Processings.SpatialBinaries;

namespace sql_storage_engine.Exposures.Spatial;
public sealed class SqlSpatialExposure : ICompositionExposure
{
    private SqlSpatialExposure()
    {
    }

    private static readonly ISqlSpatialCoordinationService service = new SqlSpatialCoordinationService(values: new SqlSpatialOrchestrationService(wkt: new SqlSpatialShapeWktProcessingService(), shapes: new SqlSpatialShapeProcessingService(), operations: new SqlSpatialShapeOperationsProcessingService()), codecs: new SqlSpatialShapeCodecOrchestrationService(wkb: new SqlSpatialShapeWkbProcessingService(), binary: new SqlSpatialShapeBinaryProcessingService(binary: new sql_storage_engine.Services.Foundations.SpatialBinaries.SqlSpatialShapeBinaryService())));
    public static SqlSpatial CreateSqlSpatial(SqlSpatialKind kind, int srid, string wellKnownText) =>
        service.CreateSqlSpatial(sqlSpatial: new SqlSpatial { Kind = kind, Srid = srid, WellKnownText = wellKnownText });

    public static string FormatSqlSpatial(SqlSpatial sqlSpatial) =>
        service.FormatSqlSpatial(sqlSpatial: sqlSpatial);

    public static string FormatSqlSpatialGml(SqlSpatial sqlSpatial) =>
        service.FormatSqlSpatialGml(sqlSpatial: sqlSpatial);

    public static string ReadSqlSpatialGeometryType(SqlSpatial sqlSpatial) =>
        service.ReadSqlSpatialGeometryType(sqlSpatial: sqlSpatial);

    public static int ReadSqlSpatialDimension(SqlSpatial sqlSpatial) =>
        service.ReadSqlSpatialDimension(sqlSpatial: sqlSpatial);

    public static bool CheckEmptySqlSpatial(SqlSpatial sqlSpatial) =>
        service.CheckEmptySqlSpatial(sqlSpatial: sqlSpatial);

    public static bool CheckValidSqlSpatial(SqlSpatial sqlSpatial) =>
        service.CheckValidSqlSpatial(sqlSpatial: sqlSpatial);

    public static int CountSqlSpatialPoints(SqlSpatial sqlSpatial) =>
        service.CountSqlSpatialPoints(sqlSpatial: sqlSpatial);

    public static double MeasureSqlSpatialLength(SqlSpatial sqlSpatial) =>
        service.MeasureSqlSpatialLength(sqlSpatial: sqlSpatial);

    public static double MeasureSqlSpatialArea(SqlSpatial sqlSpatial) =>
        service.MeasureSqlSpatialArea(sqlSpatial: sqlSpatial);

    public static (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBounds(SqlSpatial sqlSpatial) =>
        service.ReadSqlSpatialBounds(sqlSpatial: sqlSpatial);

    public static SqlSpatial CreateSqlSpatialEnvelope(SqlSpatial sqlSpatial) =>
        service.CreateSqlSpatialEnvelope(sqlSpatial: sqlSpatial);

    public static SqlSpatial? ReadSqlSpatialPoint(SqlSpatial sqlSpatial, int position) =>
        service.ReadSqlSpatialPoint(sqlSpatial: sqlSpatial, position: position);

    public static SqlSpatial? ReadSqlSpatialStartPoint(SqlSpatial sqlSpatial) =>
        service.ReadSqlSpatialStartPoint(sqlSpatial: sqlSpatial);

    public static SqlSpatial? ReadSqlSpatialEndPoint(SqlSpatial sqlSpatial) =>
        service.ReadSqlSpatialEndPoint(sqlSpatial: sqlSpatial);

    public static bool? CheckEquivalentSqlSpatials(SqlSpatial[] sqlSpatials) =>
        service.CheckEquivalentSqlSpatials(sqlSpatials: sqlSpatials);

    public static bool? CheckIntersectingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        service.CheckIntersectingSqlSpatials(sqlSpatials: sqlSpatials);

    public static bool? CheckContainingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        service.CheckContainingSqlSpatials(sqlSpatials: sqlSpatials);

    public static bool? CheckWithinSqlSpatials(SqlSpatial[] sqlSpatials) =>
        service.CheckWithinSqlSpatials(sqlSpatials: sqlSpatials);

    public static bool? CheckDisjointSqlSpatials(SqlSpatial[] sqlSpatials) =>
        service.CheckDisjointSqlSpatials(sqlSpatials: sqlSpatials);

    public static double? MeasureSqlSpatialDistance(SqlSpatial[] sqlSpatials) =>
        service.MeasureSqlSpatialDistance(sqlSpatials: sqlSpatials);

    public static byte[] EncodeSqlSpatial(SqlSpatial sqlSpatial) =>
        service.EncodeSqlSpatial(sqlSpatial: sqlSpatial);

    public static byte[] EncodeSqlSpatialWkb(SqlSpatial sqlSpatial) =>
        service.EncodeSqlSpatialWkb(sqlSpatial: sqlSpatial);

    public static SqlSpatial DecodeSqlSpatial(ReadOnlySpan<byte> value, SqlSpatialKind expectedKind) =>
        service.DecodeSqlSpatial(value: value.ToArray(), expectedKind: expectedKind);

    public static SqlSpatial DecodeSqlSpatialWkb(SqlSpatialKind kind, ReadOnlySpan<byte> value, int srid = 0) =>
        service.DecodeSqlSpatialWkb(kind: kind, value: value.ToArray(), srid: srid);
}
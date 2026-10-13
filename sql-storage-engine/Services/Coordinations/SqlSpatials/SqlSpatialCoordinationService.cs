// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Orchestrations.SqlSpatials;
using sql_storage_engine.Services.Orchestrations.SqlSpatialCodecs;

namespace sql_storage_engine.Services.Coordinations.SqlSpatials;
internal sealed partial class SqlSpatialCoordinationService : ISqlSpatialCoordinationService
{
    private readonly ISqlSpatialOrchestrationService values;
    private readonly ISqlSpatialShapeCodecOrchestrationService codecs;
    public SqlSpatialCoordinationService(ISqlSpatialOrchestrationService values, ISqlSpatialShapeCodecOrchestrationService codecs)
    {
        this.values = values;
        this.codecs = codecs;
    }

    public SqlSpatial CreateSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateCreation(sqlSpatial: sqlSpatial);
        return values.CreateSqlSpatial(sqlSpatial: sqlSpatial);
    });

    public string FormatSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.FormatSqlSpatial(sqlSpatial: sqlSpatial);
    });

    public string FormatSqlSpatialGml(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.FormatSqlSpatialGml(sqlSpatial: sqlSpatial);
    });

    public string ReadSqlSpatialGeometryType(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.ReadSqlSpatialGeometryType(sqlSpatial: sqlSpatial);
    });

    public int ReadSqlSpatialDimension(SqlSpatial sqlSpatial) =>
        TryCatch<int>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.ReadSqlSpatialDimension(sqlSpatial: sqlSpatial);
    });

    public bool CheckEmptySqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<bool>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.CheckEmptySqlSpatial(sqlSpatial: sqlSpatial);
    });

    public bool CheckValidSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<bool>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.CheckValidSqlSpatial(sqlSpatial: sqlSpatial);
    });

    public int CountSqlSpatialPoints(SqlSpatial sqlSpatial) =>
        TryCatch<int>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.CountSqlSpatialPoints(sqlSpatial: sqlSpatial);
    });

    public double MeasureSqlSpatialLength(SqlSpatial sqlSpatial) =>
        TryCatch<double>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.MeasureSqlSpatialLength(sqlSpatial: sqlSpatial);
    });

    public double MeasureSqlSpatialArea(SqlSpatial sqlSpatial) =>
        TryCatch<double>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.MeasureSqlSpatialArea(sqlSpatial: sqlSpatial);
    });

    public (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBounds(SqlSpatial sqlSpatial) =>
        TryCatch<(double MinX, double MinY, double MaxX, double MaxY)>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.ReadSqlSpatialBounds(sqlSpatial: sqlSpatial);
    });

    public SqlSpatial CreateSqlSpatialEnvelope(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.CreateSqlSpatialEnvelope(sqlSpatial: sqlSpatial);
    });

    public SqlSpatial? ReadSqlSpatialPoint(SqlSpatial sqlSpatial, int position) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidatePoint(sqlSpatial: sqlSpatial, position: position);
        return values.ReadSqlSpatialPoint(sqlSpatial: sqlSpatial, position: position);
    });

    public SqlSpatial? ReadSqlSpatialStartPoint(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.ReadSqlSpatialStartPoint(sqlSpatial: sqlSpatial);
    });

    public SqlSpatial? ReadSqlSpatialEndPoint(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return values.ReadSqlSpatialEndPoint(sqlSpatial: sqlSpatial);
    });

    public bool? CheckEquivalentSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.CheckEquivalentSqlSpatials(sqlSpatials: sqlSpatials);
    });

    public bool? CheckIntersectingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.CheckIntersectingSqlSpatials(sqlSpatials: sqlSpatials);
    });

    public bool? CheckContainingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.CheckContainingSqlSpatials(sqlSpatials: sqlSpatials);
    });

    public bool? CheckWithinSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.CheckWithinSqlSpatials(sqlSpatials: sqlSpatials);
    });

    public bool? CheckDisjointSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.CheckDisjointSqlSpatials(sqlSpatials: sqlSpatials);
    });

    public double? MeasureSqlSpatialDistance(SqlSpatial[] sqlSpatials) =>
        TryCatch<double?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return values.MeasureSqlSpatialDistance(sqlSpatials: sqlSpatials);
    });

    public byte[] EncodeSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<byte[]>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        var prepared = values.PrepareSqlSpatial(sqlSpatial: sqlSpatial);
        return codecs.EncodeSqlSpatialShape(kind: prepared.Kind, srid: prepared.Srid, sqlSpatialShape: prepared.Shape!);
    });

    public byte[] EncodeSqlSpatialWkb(SqlSpatial sqlSpatial) =>
        TryCatch<byte[]>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        var prepared = values.PrepareSqlSpatial(sqlSpatial: sqlSpatial);
        return codecs.EncodeSqlSpatialShapeWkb(sqlSpatialShape: prepared.Shape!);
    });

    public SqlSpatial DecodeSqlSpatial(byte[] value, SqlSpatialKind expectedKind) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateDecode(value: value, kind: expectedKind);
        var decoded = codecs.DecodeSqlSpatialShape(value: value);

        if (decoded.Kind != expectedKind)
        {
            throw new StorageFormatException(message: "Spatial representation has the wrong kind.");
        }

        return values.CreateSqlSpatial(sqlSpatial: new SqlSpatial { Kind = decoded.Kind, Srid = decoded.Srid, Shape = decoded.Shape });
    });

    public SqlSpatial DecodeSqlSpatialWkb(SqlSpatialKind kind, byte[] value, int srid) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateDecode(value: value, kind: kind, srid: srid);
        return values.CreateSqlSpatial(sqlSpatial: new SqlSpatial { Kind = kind, Srid = srid, Shape = codecs.DecodeSqlSpatialShapeWkb(value: value) });
    });
}
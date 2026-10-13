// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Processings.SpatialWkts;
using sql_storage_engine.Services.Processings.SpatialShapes;
using sql_storage_engine.Services.Processings.SpatialOperations;

namespace sql_storage_engine.Services.Orchestrations.SqlSpatials;
internal sealed partial class SqlSpatialOrchestrationService : ISqlSpatialOrchestrationService
{
    private readonly SqlSpatialShapeWktProcessingService wkt;
    private readonly SqlSpatialShapeProcessingService shapes;
    private readonly SqlSpatialShapeOperationsProcessingService operations;
    public SqlSpatialOrchestrationService(SqlSpatialShapeWktProcessingService wkt, SqlSpatialShapeProcessingService shapes, SqlSpatialShapeOperationsProcessingService operations)
    {
        this.wkt = wkt;
        this.shapes = shapes;
        this.operations = operations;
    }

    private SqlSpatial CreateSqlSpatialCore(SqlSpatial sqlSpatial)
    {
        var shape = sqlSpatial.Shape ?? (sqlSpatial.ShapeContent is null ? wkt.ParseSqlSpatialShape(source: sqlSpatial.WellKnownText) : DecodeShape(content: sqlSpatial.ShapeContent));

        if (shape.Type == SqlSpatialShapeType.FullGlobe && sqlSpatial.Kind != SqlSpatialKind.Geography)
        {
            throw new FormatException(message: "FULLGLOBE is valid only for geography.");
        }

        if (sqlSpatial.Kind == SqlSpatialKind.Geography)
        {
            operations.CheckSqlSpatialShapeGeography(sqlSpatialShape: shape);
        }

        return sqlSpatial with
        {
            WellKnownText = shapes.FormatSqlSpatialShape(sqlSpatialShape: shape),
            ShapeContent = EncodeShape(shape: shape),
            Shape = null
        };
    }

    public SqlSpatial CreateSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateCreation(sqlSpatial: sqlSpatial);
        return CreateSqlSpatialCore(sqlSpatial: sqlSpatial);
    });

    private SqlSpatial PrepareSqlSpatialCore(SqlSpatial sqlSpatial)
    {
        return sqlSpatial with
        {
            Shape = ReadShape(sqlSpatial: sqlSpatial)
        };
    }

    public SqlSpatial PrepareSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return PrepareSqlSpatialCore(sqlSpatial: sqlSpatial);
    });

    private string FormatSqlSpatialCore(SqlSpatial sqlSpatial)
    {
        return sqlSpatial.WellKnownText;
    }

    public string FormatSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return FormatSqlSpatialCore(sqlSpatial: sqlSpatial);
    });

    private string FormatSqlSpatialGmlCore(SqlSpatial sqlSpatial)
    {
        return shapes.FormatSqlSpatialShapeGml(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial), srid: sqlSpatial.Srid);
    }

    public string FormatSqlSpatialGml(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return FormatSqlSpatialGmlCore(sqlSpatial: sqlSpatial);
    });

    private string ReadSqlSpatialGeometryTypeCore(SqlSpatial sqlSpatial)
    {
        return ReadShape(sqlSpatial: sqlSpatial)
            .Type switch
        {
            SqlSpatialShapeType.Point => "Point",
            SqlSpatialShapeType.LineString => "LineString",
            SqlSpatialShapeType.Polygon => "Polygon",
            SqlSpatialShapeType.MultiPoint => "MultiPoint",
            SqlSpatialShapeType.MultiLineString => "MultiLineString",
            SqlSpatialShapeType.MultiPolygon => "MultiPolygon",
            SqlSpatialShapeType.GeometryCollection => "GeometryCollection",
            SqlSpatialShapeType.FullGlobe => "FullGlobe",
            SqlSpatialShapeType.CircularString => "CircularString",
            SqlSpatialShapeType.CompoundCurve => "CompoundCurve",
            SqlSpatialShapeType.CurvePolygon => "CurvePolygon",
            _ => throw new ArgumentException(message: "Unknown geometry type.")};
    }

    public string ReadSqlSpatialGeometryType(SqlSpatial sqlSpatial) =>
        TryCatch<string>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return ReadSqlSpatialGeometryTypeCore(sqlSpatial: sqlSpatial);
    });

    private int ReadSqlSpatialDimensionCore(SqlSpatial sqlSpatial)
    {
        return shapes.ReadSqlSpatialShapeDimension(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial));
    }

    public int ReadSqlSpatialDimension(SqlSpatial sqlSpatial) =>
        TryCatch<int>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return ReadSqlSpatialDimensionCore(sqlSpatial: sqlSpatial);
    });

    private bool CheckEmptySqlSpatialCore(SqlSpatial sqlSpatial)
    {
        return ReadShape(sqlSpatial: sqlSpatial)
            .IsEmpty;
    }

    public bool CheckEmptySqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<bool>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return CheckEmptySqlSpatialCore(sqlSpatial: sqlSpatial);
    });

    private bool CheckValidSqlSpatialCore(SqlSpatial sqlSpatial)
    {
        return operations.CheckValidSqlSpatialShape(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial));
    }

    public bool CheckValidSqlSpatial(SqlSpatial sqlSpatial) =>
        TryCatch<bool>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return CheckValidSqlSpatialCore(sqlSpatial: sqlSpatial);
    });

    private int CountSqlSpatialPointsCore(SqlSpatial sqlSpatial)
    {
        return shapes.FlattenSqlSpatialShape(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial))
            .Points.Length;
    }

    public int CountSqlSpatialPoints(SqlSpatial sqlSpatial) =>
        TryCatch<int>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return CountSqlSpatialPointsCore(sqlSpatial: sqlSpatial);
    });

    private double MeasureSqlSpatialLengthCore(SqlSpatial sqlSpatial)
    {
        return operations.MeasureSqlSpatialShapeLength(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial), kind: sqlSpatial.Kind);
    }

    public double MeasureSqlSpatialLength(SqlSpatial sqlSpatial) =>
        TryCatch<double>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return MeasureSqlSpatialLengthCore(sqlSpatial: sqlSpatial);
    });

    private double MeasureSqlSpatialAreaCore(SqlSpatial sqlSpatial)
    {
        return operations.MeasureSqlSpatialShapeArea(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial), kind: sqlSpatial.Kind);
    }

    public double MeasureSqlSpatialArea(SqlSpatial sqlSpatial) =>
        TryCatch<double>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return MeasureSqlSpatialAreaCore(sqlSpatial: sqlSpatial);
    });

    private (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBoundsCore(SqlSpatial sqlSpatial)
    {
        return operations.ReadSqlSpatialShapeBounds(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial));
    }

    public (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBounds(SqlSpatial sqlSpatial) =>
        TryCatch<(double MinX, double MinY, double MaxX, double MaxY)>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return ReadSqlSpatialBoundsCore(sqlSpatial: sqlSpatial);
    });

    private SqlSpatial CreateSqlSpatialEnvelopeCore(SqlSpatial sqlSpatial)
    {
        return CreateSqlSpatialCore(sqlSpatial: sqlSpatial with { Shape = operations.CreateSqlSpatialShapeEnvelope(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial)) });
    }

    public SqlSpatial CreateSqlSpatialEnvelope(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return CreateSqlSpatialEnvelopeCore(sqlSpatial: sqlSpatial);
    });

    private SqlSpatial? ReadSqlSpatialPointCore(SqlSpatial sqlSpatial, int position)
    {
        var coordinates = shapes.FlattenSqlSpatialShape(sqlSpatialShape: ReadShape(sqlSpatial: sqlSpatial))
            .Points;

        return position > coordinates.Length ? null : CreateSqlSpatialCore(sqlSpatial: sqlSpatial with { Shape = shapes.PrepareSqlSpatialShape(sqlSpatialShape: new SqlSpatialShape { Type = SqlSpatialShapeType.Point, PointSource = [coordinates[position - 1]] }) });
    }

    public SqlSpatial? ReadSqlSpatialPoint(SqlSpatial sqlSpatial, int position) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidatePoint(sqlSpatial: sqlSpatial, position: position);
        return ReadSqlSpatialPointCore(sqlSpatial: sqlSpatial, position: position);
    });

    private SqlSpatial? ReadSqlSpatialStartPointCore(SqlSpatial sqlSpatial)
    {
        return ReadSqlSpatialPointCore(sqlSpatial: sqlSpatial, position: 1);
    }

    public SqlSpatial? ReadSqlSpatialStartPoint(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return ReadSqlSpatialStartPointCore(sqlSpatial: sqlSpatial);
    });

    private SqlSpatial? ReadSqlSpatialEndPointCore(SqlSpatial sqlSpatial)
    {
        return ReadSqlSpatialPointCore(sqlSpatial: sqlSpatial, position: CountSqlSpatialPointsCore(sqlSpatial: sqlSpatial));
    }

    public SqlSpatial? ReadSqlSpatialEndPoint(SqlSpatial sqlSpatial) =>
        TryCatch<SqlSpatial?>(operation: () =>
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);
        return ReadSqlSpatialEndPointCore(sqlSpatial: sqlSpatial);
    });

    private bool? CheckEquivalentSqlSpatialsCore(SqlSpatial[] sqlSpatials)
    {
        return Compatible(sqlSpatials: sqlSpatials) ? operations.CheckEquivalentSqlSpatialShapes(sqlSpatialShapes: [ReadShape(sqlSpatial: sqlSpatials[0]), ReadShape(sqlSpatial: sqlSpatials[1])]) : null;
    }

    public bool? CheckEquivalentSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return CheckEquivalentSqlSpatialsCore(sqlSpatials: sqlSpatials);
    });

    private bool? CheckIntersectingSqlSpatialsCore(SqlSpatial[] sqlSpatials)
    {
        return Compatible(sqlSpatials: sqlSpatials) ? operations.CheckIntersectingSqlSpatialShapes(sqlSpatialShapes: [ReadShape(sqlSpatial: sqlSpatials[0]), ReadShape(sqlSpatial: sqlSpatials[1])]) : null;
    }

    public bool? CheckIntersectingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return CheckIntersectingSqlSpatialsCore(sqlSpatials: sqlSpatials);
    });

    private bool? CheckContainingSqlSpatialsCore(SqlSpatial[] sqlSpatials)
    {
        return Compatible(sqlSpatials: sqlSpatials) ? operations.CheckContainingSqlSpatialShapes(sqlSpatialShapes: [ReadShape(sqlSpatial: sqlSpatials[0]), ReadShape(sqlSpatial: sqlSpatials[1])]) : null;
    }

    public bool? CheckContainingSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return CheckContainingSqlSpatialsCore(sqlSpatials: sqlSpatials);
    });

    private bool? CheckWithinSqlSpatialsCore(SqlSpatial[] sqlSpatials)
    {
        return Compatible(sqlSpatials: sqlSpatials) ? operations.CheckContainingSqlSpatialShapes(sqlSpatialShapes: [ReadShape(sqlSpatial: sqlSpatials[1]), ReadShape(sqlSpatial: sqlSpatials[0])]) : null;
    }

    public bool? CheckWithinSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return CheckWithinSqlSpatialsCore(sqlSpatials: sqlSpatials);
    });

    private bool? CheckDisjointSqlSpatialsCore(SqlSpatial[] sqlSpatials)
    {
        var intersects = CheckIntersectingSqlSpatialsCore(sqlSpatials: sqlSpatials);
        return intersects.HasValue ? !intersects.Value : null;
    }

    public bool? CheckDisjointSqlSpatials(SqlSpatial[] sqlSpatials) =>
        TryCatch<bool?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return CheckDisjointSqlSpatialsCore(sqlSpatials: sqlSpatials);
    });

    private double? MeasureSqlSpatialDistanceCore(SqlSpatial[] sqlSpatials)
    {
        return Compatible(sqlSpatials: sqlSpatials) ? operations.MeasureSqlSpatialShapeDistance(sqlSpatialShapes: [ReadShape(sqlSpatial: sqlSpatials[0]), ReadShape(sqlSpatial: sqlSpatials[1])], kind: sqlSpatials[0].Kind) : null;
    }

    public double? MeasureSqlSpatialDistance(SqlSpatial[] sqlSpatials) =>
        TryCatch<double?>(operation: () =>
    {
        ValidateSqlSpatials(sqlSpatials: sqlSpatials);
        return MeasureSqlSpatialDistanceCore(sqlSpatials: sqlSpatials);
    });

    private SqlSpatialShape ReadShape(SqlSpatial sqlSpatial) =>
        sqlSpatial.Shape ?? (sqlSpatial.ShapeContent is null ? wkt.ParseSqlSpatialShape(source: sqlSpatial.WellKnownText) : DecodeShape(content: sqlSpatial.ShapeContent));

    private static bool Compatible(SqlSpatial[] sqlSpatials) =>
        sqlSpatials[0].Kind == sqlSpatials[1].Kind && sqlSpatials[0].Srid == sqlSpatials[1].Srid;

    private string EncodeShape(SqlSpatialShape shape) =>
        shapes.WriteSqlSpatialShapeContent(sqlSpatialShape: shape);

    private SqlSpatialShape DecodeShape(string content) =>
        shapes.ReadSqlSpatialShapeContent(content: content);

    internal static SqlSpatialOrchestrationService CreateSqlSpatialOrchestrationService() =>
        new SqlSpatialOrchestrationService(wkt: new sql_storage_engine.Services.Processings.SpatialWkts.SqlSpatialShapeWktProcessingService(), shapes: new sql_storage_engine.Services.Processings.SpatialShapes.SqlSpatialShapeProcessingService(), operations: new sql_storage_engine.Services.Processings.SpatialOperations.SqlSpatialShapeOperationsProcessingService());
}
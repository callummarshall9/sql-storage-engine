// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.SqlHierarchies;
using sql_storage_engine.Services.Foundations.SpatialBinaries;
using sql_storage_engine.Services.Foundations.SqlClrContexts;

namespace sql_storage_engine.Services.Orchestrations.SqlTypeValues;
internal sealed partial class SqlTypeValueContextOrchestrationService : ISqlTypeValueContextOrchestrationService
{
    private readonly SqlHierarchyIdService hierarchies;
    private readonly SqlSpatialShapeBinaryService spatial;
    private readonly SqlClrContextService clr;
    public SqlTypeValueContextOrchestrationService(SqlHierarchyIdService hierarchies, SqlSpatialShapeBinaryService spatial, SqlClrContextService clr)
    {
        this.hierarchies = hierarchies;
        this.spatial = spatial;
        this.clr = clr;
    }

    public SqlTypeContext CheckSqlTypeContext(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        var type = sqlTypeContext.SqlTypeSource!;
        var input = sqlTypeContext.InputSqlValueValue!;
        var column = sqlTypeContext.InputstringColumnName!;

        if (input.IsNull)
        {
            return sqlTypeContext;
        }

        if (input is HierarchyIdSqlValue hierarchy)
        {
            var bytes = hierarchies.WriteSqlHierarchyId(sqlHierarchyId: hierarchy.Value);

            if (bytes.Length > 892)
            {
                throw new ArgumentOutOfRangeException(paramName: "length", message: $"Column '{column}' exceeds hierarchyid's 892-byte representation limit.");
            }
        }

        if (input is BinarySqlValue binary)
        {
            var bytes = binary.Content.Select(selector: item => checked((byte)item))
            .ToArray();

            if (type.UserTypeKind == SqlUserTypeKind.Clr)
            {
                var result = clr.ReadSqlClrContextValidation(sqlClrContext: new SqlClrContext { SqlType = type, Value = bytes });

                if (result.Found && !result.Valid)
                {
                    throw new ArgumentException(message: $"Column '{column}' failed CLR UDT validation.", paramName: "value");
                }
            }
            else if (type.Name == SqlTypeName.HierarchyId)
            {
                _ = hierarchies.ReadSqlHierarchyId(value: bytes);
            }
            else if (type.Name is SqlTypeName.Geometry or SqlTypeName.Geography)
            {
                var decoded = spatial.DecodeSqlSpatialShape(value: bytes);
                var expected = type.Name == SqlTypeName.Geometry ? SqlSpatialKind.Geometry : SqlSpatialKind.Geography;

                if (decoded.Shape.Type == sql_storage_engine.Rows.SqlSpatialShapeType.FullGlobe && decoded.Kind != SqlSpatialKind.Geography)
                {
                    throw new FormatException(message: "FULLGLOBE is valid only for geography.");
                }

                if (decoded.Kind == SqlSpatialKind.Geography)
                {
                    ValidateGeography(shape: decoded.Shape);
                }

                if (decoded.Kind != expected)
                {
                    throw new sql_storage_engine.Storage.StorageFormatException(message: "Spatial binary kind does not match the declared SQL type.");
                }
            }
        }

        return sqlTypeContext;
    });

    private static System.Collections.Generic.IEnumerable<sql_storage_engine.Models.Spatial.SqlSpatialCoordinate> Coordinates(sql_storage_engine.Models.Spatial.SqlSpatialShape shape) =>
        shape.Points.Concat(second: shape.Children.SelectMany(selector: child => Coordinates(shape: child)));

    private static void ValidateGeography(sql_storage_engine.Models.Spatial.SqlSpatialShape shape)
    {
        foreach (var point in Coordinates(shape: shape))
        {
            if (point.X is < -180 or > 180 || point.Y is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(shape), message: "Geography longitude/latitude is outside its valid range.");
            }
        }
    }

    public SqlTypeContext ProcessSqlTypeContextHierarchyParse(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            HierarchyResult = hierarchies.ParseSqlHierarchyId(value: sqlTypeContext.TextResult!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextHierarchyRead(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            HierarchyResult = hierarchies.ReadSqlHierarchyId(value: sqlTypeContext.BinarySource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextHierarchyFormat(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            TextResult = hierarchies.FormatSqlHierarchyId(sqlHierarchyId: sqlTypeContext.HierarchySource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSpatialRead(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlTypeContext: sqlTypeContext);
        var decoded = spatial.DecodeSqlSpatialShape(value: sqlTypeContext.BinarySource!);
        var expected = sqlTypeContext.SqlTypeSource!.Name == SqlTypeName.Geometry ? SqlSpatialKind.Geometry : SqlSpatialKind.Geography;

        if (decoded.Kind != expected)
        {
            throw new sql_storage_engine.Storage.StorageFormatException(message: "Spatial representation has the wrong kind.");
        }

        return sqlTypeContext with
        {
            SpatialResult = new SqlSpatial
            {
                Kind = decoded.Kind,
                Srid = decoded.Srid,
                Shape = decoded.Shape
            }
        };
    });

    internal static SqlTypeValueContextOrchestrationService CreateSqlTypeValueContextOrchestrationService() =>
        new SqlTypeValueContextOrchestrationService(hierarchies: new sql_storage_engine.Services.Foundations.SqlHierarchies.SqlHierarchyIdService(), spatial: new sql_storage_engine.Services.Foundations.SpatialBinaries.SqlSpatialShapeBinaryService(), clr: sql_storage_engine.Services.Foundations.SqlClrContexts.SqlClrContextService.CreateSqlClrContextService());
}
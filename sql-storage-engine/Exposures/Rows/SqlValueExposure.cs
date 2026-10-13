// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using System;
using System.Collections.Generic;
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Catalog;
using sql_storage_engine.Services.Aggregations.SqlValues;

namespace sql_storage_engine.Exposures.Rows;
public sealed class SqlValueExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private SqlValueExposure()
    {
    }

    private static readonly ISqlValueRequestAggregationService service = SqlValueRequestAggregationService.CreateSqlValueRequestAggregationService();
    public static SqlValue ReadNullSqlValue() =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Null", Input = null, Other = null })
            .Result!;

    public static SqlValue ReadDefaultSqlValue() =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Default", Input = null, Other = null })
            .Result!;

    public static SqlValue CreateBooleanSqlValue(bool value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Boolean", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateIntegerSqlValue(long value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Integer", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateTextSqlValue(string value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Text", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateFloatSqlValue(double value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Float", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateDateSqlValue(DateOnly value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Date", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateTimeSqlValue(TimeOnly value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Time", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateDateTimeSqlValue(System.DateTime value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "DateTime", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateDateTimeOffsetSqlValue(System.DateTimeOffset value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "DateTimeOffset", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateUniqueIdentifierSqlValue(Guid value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "UniqueIdentifier", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Binary", Input = value.ToArray(), Other = null })
            .Result!;

    public static SqlValue CreateDecimalSqlValue(decimal value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "DecimalClr", Input = value, Other = null })
            .Result!;

    public static SqlValue CreateDecimalSqlValue(SqlDecimal sqlDecimal) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Decimal", Input = sqlDecimal, Other = null })
            .Result!;

    public static SqlValue CreateDecimalSqlValue(BigInteger coefficient, byte scale) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "DecimalParts", Input = coefficient, Other = scale })
            .Result!;

    public static SqlValue CreateVariantSqlValue(SqlType sqlType, SqlValue sqlValue) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Variant", Input = sqlType, Other = sqlValue })
            .Result!;

    public static SqlValue CreateVectorSqlValue(IEnumerable<float> values) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Vector", Input = values, Other = null })
            .Result!;

    public static SqlValue CreateHalfVectorSqlValue(IEnumerable<Half> values) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "HalfVector", Input = values, Other = null })
            .Result!;

    public static SqlValue CreateHierarchyIdSqlValue(string path) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "HierarchyPath", Input = path, Other = null })
            .Result!;

    public static SqlValue CreateHierarchyIdSqlValue(SqlHierarchyId sqlHierarchyId) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Hierarchy", Input = sqlHierarchyId, Other = null })
            .Result!;

    public static SqlValue CreateGeometrySqlValue(string wellKnownText, int srid = 0) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Geometry", Input = wellKnownText, Other = null, Srid = srid })
            .Result!;

    public static SqlValue CreateGeographySqlValue(string wellKnownText, int srid = 4326) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Geography", Input = wellKnownText, Other = null, Srid = srid })
            .Result!;

    public static SqlValue CreateCursorSqlValue(IReadOnlyList<Row> rows) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Cursor", Input = rows, Other = null })
            .Result!;

    public static SqlValue CreateTableSqlValue(TableDefinition tableDefinition, IEnumerable<Row> rows) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Table", Input = tableDefinition, Other = rows })
            .Result!;

    public static SqlValue CreateTableSqlValue(CatalogTableType catalogTableType, IEnumerable<Row> rows) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "CatalogTable", Input = catalogTableType, Other = rows })
            .Result!;

    public static SqlValue CreateFromSqlValue(object? value) =>
        service.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "From", Input = value, Other = null })
            .Result!;

    public static SqlComparison CompareSqlValues(SqlValue[] sqlValues) =>
        service.CompareSqlValueRequest(sqlValueRequest: new SqlValueRequest { SqlValues = sqlValues })
            .Comparison;

    public static ReadOnlyMemory<byte> ReadBinarySqlValue(BinarySqlValue binarySqlValue) =>
        service.ReadBinarySqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = binarySqlValue })
            .BytesResult!;

    public static IReadOnlyList<float> ReadVectorSqlValueValues(VectorSqlValue vectorSqlValue) =>
        service.ReadVectorSqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = vectorSqlValue })
            .FloatResult!;

    internal static IReadOnlyList<Half> ReadVectorSqlValueHalfValues(VectorSqlValue vectorSqlValue) =>
        service.ReadVectorSqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = vectorSqlValue })
            .HalfResult!;

    public static double MeasureVectorSqlValueDistance(VectorSqlValue[] vectorSqlValues, SqlVectorDistanceMetric metric = SqlVectorDistanceMetric.Cosine) =>
        service.MeasureVectorSqlValueRequest(sqlValueRequest: new SqlValueRequest { SqlValues = vectorSqlValues, Other = metric })
            .DistanceResult;
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Coordinations.SqlTypes;
using sql_storage_engine.Services.Coordinations.TableDefinitions;
using sql_storage_engine.Services.Coordinations.SqlValueComparisons;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Services.Managements.SqlValues;
internal sealed partial class SqlValueRequestManagementService : ISqlValueRequestManagementService
{
    private readonly SqlTypeContextCoordinationService types;
    private readonly SqlValueComparisonCoordinationService comparison;
    private readonly TableDefinitionCoordinationService definitions;
    private SqlValueRequestManagementService(SqlTypeContextCoordinationService types, SqlValueComparisonCoordinationService comparison, TableDefinitionCoordinationService definitions)
    {
        this.types = types;
        this.comparison = comparison;
        this.definitions = definitions;
    }

    internal static SqlValueRequestManagementService CreateSqlValueRequestManagementService() =>
        new(types: SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), comparison: SqlValueComparisonCoordinationService.CreateSqlValueComparisonCoordinationService(), definitions: TableDefinitionCoordinationService.CreateTableDefinitionCoordinationService());

    public SqlValueRequest ConvertSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlValueRequest: sqlValueRequest);

        return sqlValueRequest with
        {
            Result = sqlValueRequest.Third is SqlType source ? types.ProcessSqlTypeContextConvert79(sqlTypeContext: new SqlTypeContext { SqlTypeSource = (SqlType)sqlValueRequest.Input!, InputSqlTypeSource = source, InputSqlValueValue = (SqlValue)sqlValueRequest.Other! })
            .SqlValueResult! : types.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = (SqlType)sqlValueRequest.Input!, InputSqlValueValue = (SqlValue)sqlValueRequest.Other! })
            .SqlValueResult!
        };
    });

    public SqlValueRequest ValidateSqlValueRequestRow(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlValueRequest: sqlValueRequest);
        definitions.CheckTableDefinitionRow(tableDefinition: new TableDefinition { SchemaSource = (TableDefinition)sqlValueRequest.Input!, RowInput = (Row)sqlValueRequest.Other! });
        return sqlValueRequest;
    });

    public SqlValueRequest CreateSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(sqlValueRequest: sqlValueRequest);
        var value = sqlValueRequest.Input;

        var result = sqlValueRequest.Operation switch
        {
            "Null" => nullValue,
            "Default" => defaultValue,
            "Boolean" => CreateBooleanSqlValue(value: (bool)value!),
            "Integer" => CreateIntegerSqlValue(value: (long)value!),
            "Text" => CreateTextSqlValue(value: (string)value!),
            "Binary" => CreateBinarySqlValue(value: (byte[])value!),
            "DecimalClr" => CreateDecimalSqlValue(value: (decimal)value!),
            "Decimal" => CreateDecimalSqlValue(value: (SqlDecimal)value!),
            "DecimalParts" => CreateDecimalSqlValue(coefficient: (BigInteger)value!, scale: (byte)sqlValueRequest.Other!),
            "Float" => CreateFloatSqlValue(value: (double)value!),
            "Date" => CreateDateSqlValue(value: (DateOnly)value!),
            "Time" => CreateTimeSqlValue(value: (TimeOnly)value!),
            "DateTime" => CreateDateTimeSqlValue(value: (System.DateTime)value!),
            "DateTimeOffset" => CreateDateTimeOffsetSqlValue(value: (System.DateTimeOffset)value!),
            "UniqueIdentifier" => CreateUniqueIdentifierSqlValue(value: (Guid)value!),
            "Variant" => CreateVariantSqlValue(declaredType: (SqlType)value!, value: (SqlValue)sqlValueRequest.Other!),
            "Vector" => CreateVectorSqlValue(values: (IEnumerable<float>)value!),
            "HalfVector" => CreateHalfVectorSqlValue(values: (IEnumerable<Half>)value!),
            "HierarchyPath" => CreateHierarchyIdSqlValue(path: (string)value!),
            "Hierarchy" => CreateHierarchyIdSqlValue(value: (SqlHierarchyId)value!),
            "Geometry" => CreateGeometrySqlValue(wellKnownText: (string)value!, srid: sqlValueRequest.Srid),
            "Geography" => CreateGeographySqlValue(wellKnownText: (string)value!, srid: sqlValueRequest.Srid),
            "Cursor" => CreateCursorSqlValue(rows: (IReadOnlyList<Row>)value!),
            "From" => CreateFromSqlValue(value: value),
            _ => throw new ArgumentException(message: "Unknown SQL value creation operation.", paramName: nameof(sqlValueRequest))};

        return sqlValueRequest with
        {
            Result = result
        };
    });

    public SqlValueRequest CompareSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateComparison(sqlValueRequest: sqlValueRequest);

        return sqlValueRequest with
        {
            Comparison = comparison.CompareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = sqlValueRequest.SqlValues![0], Right = sqlValueRequest.SqlValues![1] })
            .Comparison
        };
    });
    private static readonly SqlValue nullValue = new NullSqlValue
    {
        IsNull = true
    };
    private static readonly SqlValue defaultValue = new DefaultSqlValue();
    private SqlValue CreateBooleanSqlValue(bool value) =>
        new BooleanSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.Boolean
    };

    private SqlValue CreateIntegerSqlValue(long value) =>
        new IntegerSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.Integer
    };

    private SqlValue CreateTextSqlValue(string value) =>
        new TextSqlValue
    {
        Value = value ?? throw new ArgumentNullException(paramName: nameof(value)),
        StorageFamily = SqlStorageFamily.Text
    };

    private SqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        new BinarySqlValue
    {
        Content = sql_storage_engine.Brokers.BinaryPayloads.BinaryPayloadBroker.ReadBinaryContent(bytes: value),
        StorageFamily = SqlStorageFamily.Binary
    };

    private SqlValue CreateDecimalSqlValue(decimal value) =>
        new sql_storage_engine.Models.Rows.DecimalSqlValue
    {
        Value = types.ProcessSqlTypeContextDecimalParse(sqlTypeContext: new SqlTypeContext { TextResult = value.ToString(provider: System.Globalization.CultureInfo.InvariantCulture) })
            .DecimalResult!.Value,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Decimal
    };

    private SqlValue CreateDecimalSqlValue(SqlDecimal value) =>
        new sql_storage_engine.Models.Rows.DecimalSqlValue
    {
        Value = value,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Decimal
    };

    private SqlValue CreateDecimalSqlValue(System.Numerics.BigInteger coefficient, byte scale) =>
        new sql_storage_engine.Models.Rows.DecimalSqlValue
    {
        Value = types.ProcessSqlTypeContextDecimalCreate(sqlTypeContext: new SqlTypeContext { CoefficientSource = coefficient, DecimalScale = scale })
            .DecimalResult!.Value,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Decimal
    };

    private SqlValue CreateFloatSqlValue(double value)
    {
        if (!double.IsFinite(d: value))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(value), message: "SQL floating-point values must be finite.");
        }

        return new FloatSqlValue
        {
            Value = value == 0d ? 0d : value,
            StorageFamily = SqlStorageFamily.Float
        };
    }

    private SqlValue CreateDateSqlValue(DateOnly value) =>
        new DateSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.Date
    };

    private SqlValue CreateTimeSqlValue(TimeOnly value) =>
        new TimeSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.Time
    };

    private SqlValue CreateDateTimeSqlValue(System.DateTime value) =>
        new DateTimeSqlValue
    {
        Value = new System.DateTime(ticks: value.Ticks, kind: DateTimeKind.Unspecified),
        StorageFamily = SqlStorageFamily.DateTime
    };

    private SqlValue CreateDateTimeOffsetSqlValue(System.DateTimeOffset value) =>
        new DateTimeOffsetSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.DateTimeOffset
    };

    private SqlValue CreateUniqueIdentifierSqlValue(Guid value) =>
        new UniqueIdentifierSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.UniqueIdentifier
    };

    private SqlValue CreateVariantSqlValue(SqlType declaredType, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(argument: declaredType);
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value.IsNull)
        {
            throw new ArgumentException(message: "sql_variant cannot wrap SQL NULL.", paramName: nameof(value));
        }

        if (!types.ProcessSqlTypeContextCanBeSqlVariantValue6(sqlTypeContext: new SqlTypeContext { SqlTypeSource = declaredType })
            .BooleanResult)
        {
            throw new ArgumentException(message: $"{types.ProcessSqlTypeContextToString80(sqlTypeContext: new SqlTypeContext { SqlTypeSource = declaredType })
            .TextResult} cannot be stored in sql_variant.", paramName: nameof(declaredType));
        }

        types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = declaredType, InputSqlValueValue = value, InputstringColumnName = "sql_variant" });

        return new VariantSqlValue
        {
            DeclaredType = declaredType,
            StorageFamily = SqlStorageFamily.Variant,
            Value = types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = declaredType, InputSqlValueValue = value })
            .SqlValueResult!
        };
    }

    private SqlValue CreateHierarchyIdSqlValue(string path) =>
        new sql_storage_engine.Models.Rows.HierarchyIdSqlValue
    {
        Value = types.ProcessSqlTypeContextHierarchyParse(sqlTypeContext: new SqlTypeContext { TextResult = path })
            .HierarchyResult!,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Binary
    };

    private SqlValue CreateHierarchyIdSqlValue(SqlHierarchyId value) =>
        new sql_storage_engine.Models.Rows.HierarchyIdSqlValue
    {
        Value = value ?? throw new ArgumentNullException(paramName: nameof(value)),
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Binary
    };

    private SqlValue CreateGeometrySqlValue(string wellKnownText, int srid = 0) =>
        new sql_storage_engine.Models.Rows.SpatialSqlValue
    {
        Value = types.ProcessSqlTypeContextSpatialCreate(sqlTypeContext: new SqlTypeContext { SpatialSource = new SqlSpatial { Kind = SqlSpatialKind.Geometry, Srid = srid, WellKnownText = wellKnownText } })
            .SpatialResult!,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Binary
    };

    private SqlValue CreateGeographySqlValue(string wellKnownText, int srid = 4326) =>
        new sql_storage_engine.Models.Rows.SpatialSqlValue
    {
        Value = types.ProcessSqlTypeContextSpatialCreate(sqlTypeContext: new SqlTypeContext { SpatialSource = new SqlSpatial { Kind = SqlSpatialKind.Geography, Srid = srid, WellKnownText = wellKnownText } })
            .SpatialResult!,
        StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Binary
    };

    private SqlValue CreateCursorSqlValue(IReadOnlyList<Row> rows) =>
        new CursorSqlValue
    {
        Rows = Array.AsReadOnly(array: (rows ?? throw new ArgumentNullException(paramName: nameof(rows))).ToArray()),
        Position = -1,
        IsOpen = true
    };

    private SqlValue CreateFromSqlValue(object? value) =>
        value switch
    {
        null => nullValue,
        bool boolean => CreateBooleanSqlValue(value: boolean),
        byte integer => CreateIntegerSqlValue(value: integer),
        sbyte integer => CreateIntegerSqlValue(value: integer),
        short integer => CreateIntegerSqlValue(value: integer),
        ushort integer => CreateIntegerSqlValue(value: integer),
        int integer => CreateIntegerSqlValue(value: integer),
        uint integer => CreateIntegerSqlValue(value: integer),
        long integer => CreateIntegerSqlValue(value: integer),
        ulong integer when integer <= long.MaxValue => CreateIntegerSqlValue(value: (long)integer),
        string text => CreateTextSqlValue(value: text),
        byte[] bytes => CreateBinarySqlValue(value: bytes),
        ReadOnlyMemory<byte> bytes => CreateBinarySqlValue(value: bytes.Span),
        decimal exact => CreateDecimalSqlValue(value: exact),
        double approximate => CreateFloatSqlValue(value: approximate),
        float approximate => CreateFloatSqlValue(value: approximate),
        DateOnly date => CreateDateSqlValue(value: date),
        TimeOnly time => CreateTimeSqlValue(value: time),
        System.DateTime dateTime => CreateDateTimeSqlValue(value: dateTime),
        System.DateTimeOffset dateTimeOffset => CreateDateTimeOffsetSqlValue(value: dateTimeOffset),
        Guid identifier => CreateUniqueIdentifierSqlValue(value: identifier),
        _ => throw new ArgumentException(message: $"Unsupported SQL runtime representation '{value.GetType()
            .FullName}'.", paramName: nameof(value))};

    public SqlValueRequest ReadBinarySqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateBinaryRead(sqlValueRequest: sqlValueRequest);
        var binary = (BinarySqlValue)sqlValueRequest.Input!;

        return sqlValueRequest with
        {
            BytesResult = binary.Content.Select(selector: item => checked((byte)item))
            .ToArray()
        };
    });

    public SqlValueRequest ReadVectorSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateVectorRead(sqlValueRequest: sqlValueRequest);
        var vector = (VectorSqlValue)sqlValueRequest.Input!;

        return sqlValueRequest with
        {
            FloatResult = Array.AsReadOnly(array: ReadVector(vectorSqlValue: vector)),
            HalfResult = Array.AsReadOnly(array: ReadHalfVector(vectorSqlValue: vector))
        };
    });

    public SqlValueRequest MeasureVectorSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateVectorDistance(sqlValueRequest: sqlValueRequest);
        var vectors = sqlValueRequest.SqlValues!;
        var left = ReadVector(vectorSqlValue: (VectorSqlValue)vectors[0]);
        var right = ReadVector(vectorSqlValue: (VectorSqlValue)vectors[1]);

        if (left.Length != right.Length)
        {
            throw new ArgumentException(message: "Vector dimensions must match.", paramName: "other");
        }

        double dot = 0, leftSquared = 0, rightSquared = 0, squaredDistance = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftSquared += left[i] * left[i];
            rightSquared += right[i] * right[i];
            var difference = left[i] - right[i];
            squaredDistance += difference * difference;
        }

        var metric = (SqlVectorDistanceMetric)sqlValueRequest.Other!;

        var distance = metric switch
        {
            SqlVectorDistanceMetric.DotProduct => -dot,
            SqlVectorDistanceMetric.Euclidean => Math.Sqrt(d: squaredDistance),
            SqlVectorDistanceMetric.Cosine when leftSquared == 0 || rightSquared == 0 => double.NaN,
            SqlVectorDistanceMetric.Cosine => 1d - dot / Math.Sqrt(d: leftSquared * rightSquared),
            _ => throw new ArgumentOutOfRangeException(paramName: "metric")};

        return sqlValueRequest with
        {
            DistanceResult = distance
        };
    });

    private static SqlValue CreateVectorSqlValue(IEnumerable<float> values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
        var snapshot = values.ToArray();

        if (snapshot.Any(predicate: value => !float.IsFinite(f: value)))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(values), message: "SQL vector elements must be finite.");
        }

        var content = new char[snapshot.Length * 2];

        for (var i = 0; i < snapshot.Length; i++)
        {
            var bits = BitConverter.SingleToUInt32Bits(value: snapshot[i] == 0f ? 0f : snapshot[i]);
            content[i * 2] = (char)(bits & 65535);
            content[i * 2 + 1] = (char)(bits >> 16);
        }

        return new VectorSqlValue
        {
            BaseType = SqlVectorBaseType.Float32,
            Content = new string (value: content),
            StorageFamily = SqlStorageFamily.Vector
        };
    }

    private static SqlValue CreateHalfVectorSqlValue(IEnumerable<Half> values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
        var snapshot = values.ToArray();

        if (snapshot.Any(predicate: value => !Half.IsFinite(value: value)))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(values), message: "SQL vector elements must be finite.");
        }

        var content = snapshot.Select(selector: value => (char)BitConverter.HalfToUInt16Bits(value: value == (Half)0 ? (Half)0 : value))
            .ToArray();

        return new VectorSqlValue
        {
            BaseType = SqlVectorBaseType.Float16,
            Content = new string (value: content),
            StorageFamily = SqlStorageFamily.Vector
        };
    }

    private static float[] ReadVector(VectorSqlValue vectorSqlValue)
    {
        if (vectorSqlValue.BaseType == SqlVectorBaseType.Float16)
        {
            return ReadHalfVector(vectorSqlValue: vectorSqlValue)
            .Select(selector: value => (float)value)
            .ToArray();
        }

        var content = vectorSqlValue.Content;
        var values = new float[content.Length / 2];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BitConverter.UInt32BitsToSingle(value: (uint)content[i * 2] | ((uint)content[i * 2 + 1] << 16));
        }

        return values;
    }

    private static Half[] ReadHalfVector(VectorSqlValue vectorSqlValue) =>
        vectorSqlValue.BaseType == SqlVectorBaseType.Float16 ? vectorSqlValue.Content.Select(selector: item => BitConverter.UInt16BitsToHalf(value: item))
            .ToArray() : [];

    public SqlValueRequest PrepareSqlValueRequestDefinition(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(sqlValueRequest: sqlValueRequest);

        return sqlValueRequest with
        {
            DefinitionResult = definitions.PrepareTableDefinition(tableDefinition: new TableDefinition { ColumnSource = (IEnumerable<ColumnDefinition>)sqlValueRequest.Input! })
        };
    });
}
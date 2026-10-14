// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Globalization;
using System.Numerics;
using System.Collections.Generic;
using sql_storage_engine.Rows;
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlTypes;
internal sealed partial class SqlTypeContextCoordinationService
{
    private SqlValue ExecuteAssignment(SqlType target, SqlValue value) =>
        ConvertAssignmentToCore(target: target, source: null, value: value);
    /// <summary>Converts a value while retaining source-declaration rules such as money-to-integer rounding.</summary>
    private SqlValue ExecuteAssignment(SqlType target, SqlType source, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(argument: source);
        ArgumentNullException.ThrowIfNull(argument: value);
        var effectiveSource = source.UserTypeKind == SqlUserTypeKind.Alias ? source.BaseType! : source;
        CheckSqlTypeValue(sqlType: source, sqlValue: value, columnName: "conversion source");
        return ConvertAssignmentToCore(target: target, source: effectiveSource, value: NormalizeSqlTypeValue(sqlType: source, sqlValue: value));
    }

    private SqlValue ConvertAssignmentToCore(SqlType target, SqlType? source, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(argument: target);
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value is DefaultSqlValue)
        {
            throw new ArgumentException(message: "DEFAULT must be resolved before conversion.", paramName: nameof(value));
        }

        if (value.IsNull)
        {
            return value;
        }

        var effective = target.UserTypeKind == SqlUserTypeKind.Alias ? target.BaseType! : target;
        SqlValue converted;

        try
        {
            converted = value.StorageFamily == ReadSqlTypeStorageFamily(sqlType: effective) && CompatibleRuntime(target: effective, value: value) ? value : ConvertCore(target: effective, source: source, value: value);
            CheckSqlTypeValue(sqlType: target, sqlValue: converted, columnName: "assignment");
            return NormalizeSqlTypeValue(sqlType: target, sqlValue: converted);
        }
        catch (Exception exception)when (exception is FormatException or OverflowException or InvalidCastException)
        {
            throw new ArgumentException(message: $"Value cannot be converted to {FormatSqlType(sqlType: target)}.", paramName: nameof(value), innerException: exception);
        }
    }

    private bool CompatibleRuntime(SqlType target, SqlValue value) =>
        (ReadSqlTypeStorageFamily(sqlType: target), value) switch
    {
        (SqlStorageFamily.Binary, HierarchyIdSqlValue) => target.Name == SqlTypeName.HierarchyId,
        (SqlStorageFamily.Binary, SpatialSqlValue spatial) => target.Name == SqlTypeName.Geometry && spatial.Value.Kind == SqlSpatialKind.Geometry || target.Name == SqlTypeName.Geography && spatial.Value.Kind == SqlSpatialKind.Geography,
        (SqlStorageFamily.Binary, BinarySqlValue) => target.Name is not (SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography),
        (SqlStorageFamily.Text, TextSqlValue) => true,
        (SqlStorageFamily.Vector, VectorSqlValue vector) => vector.BaseType == target.VectorBaseType,
        (_, VariantSqlValue) => target.Name == SqlTypeName.SqlVariant,
        _ => true
    };

    private SqlValue ConvertCore(SqlType target, SqlType? source, SqlValue value)
    {
        if (target.Name == SqlTypeName.SqlVariant)
        {
            return value is VariantSqlValue ? value : CreateVariantSqlValue(sqlType: InferType(value: value), sqlValue: value);
        }

        return ReadSqlTypeStorageFamily(sqlType: target) switch
        {
            SqlStorageFamily.Boolean => CreateBooleanSqlValue(value: ToBoolean(value: value)),
            SqlStorageFamily.Integer => CreateIntegerSqlValue(value: ToInt64(value: value, source: source)),
            SqlStorageFamily.Decimal => CreateDecimalSqlValue(sqlDecimal: ToDecimal(value: value)),
            SqlStorageFamily.Float => CreateFloatSqlValue(value: ToDouble(value: value)),
            SqlStorageFamily.Text => CreateTextSqlValue(value: ToText(value: value)),
            SqlStorageFamily.Binary => ToBinaryTarget(target: target, value: value),
            SqlStorageFamily.UniqueIdentifier => CreateUniqueIdentifierSqlValue(value: ToGuid(value: value)),
            SqlStorageFamily.Date => CreateDateSqlValue(value: ToDate(value: value)),
            SqlStorageFamily.Time => CreateTimeSqlValue(value: ToTime(value: value)),
            SqlStorageFamily.DateTime => CreateDateTimeSqlValue(value: ToDateTime(value: value)),
            SqlStorageFamily.DateTimeOffset => CreateDateTimeOffsetSqlValue(value: ToDateTimeOffset(value: value)),
            SqlStorageFamily.Vector => ToVector(target: target, value: value),
            _ => throw new InvalidCastException()};
    }

    private bool ToBoolean(SqlValue value) =>
        value switch
    {
        BooleanSqlValue boolean => boolean.Value,
        IntegerSqlValue integer => integer.Value != 0,
        DecimalSqlValue exact => !exact.Value.Coefficient.IsZero,
        FloatSqlValue approximate => approximate.Value != 0,
        TextSqlValue text when bool.TryParse(value: text.Value, result: out var boolean) => boolean,
        TextSqlValue text => ParseSqlDecimal(value: text.Value)
            .Coefficient != 0,
        _ => throw new InvalidCastException()};

    private long ToInt64(SqlValue value, SqlType? source) =>
        value switch
    {
        BooleanSqlValue boolean => boolean.Value ? 1 : 0,
        IntegerSqlValue integer => integer.Value,
        DecimalSqlValue exact when source?.Name is SqlTypeName.Money or SqlTypeName.SmallMoney => RoundExactToInt64(value: exact.Value),
        DecimalSqlValue exact => checked((long)(exact.Value.Coefficient / BigInteger.Pow(value: 10, exponent: exact.Value.Scale))),
        FloatSqlValue approximate => checked((long)Math.Truncate(d: approximate.Value)),
        DateTimeSqlValue dateTime when source?.Name is SqlTypeName.DateTime or SqlTypeName.SmallDateTime => checked((long)Math.Round(value: (dateTime.Value - new DateTime(year: 1900, month: 1, day: 1)).TotalDays, mode: MidpointRounding.AwayFromZero)),
        TextSqlValue text => long.Parse(s: text.Value, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()};

    private long RoundExactToInt64(SqlDecimal value)
    {
        var coefficient = types.ProcessSqlTypeContextDecimalRound(sqlTypeContext: new SqlTypeContext { DecimalSource = value, DecimalScale = 0 })
            .CoefficientResult;

        return checked((long)coefficient);
    }

    private SqlDecimal ToDecimal(SqlValue value) =>
        value switch
    {
        BooleanSqlValue boolean => CreateSqlDecimal(coefficient: boolean.Value ? BigInteger.One : BigInteger.Zero, scale: 0),
        IntegerSqlValue integer => CreateSqlDecimal(coefficient: integer.Value, scale: 0),
        DecimalSqlValue exact => exact.Value,
        FloatSqlValue approximate => ParseFloatingDecimal(value: approximate.Value),
        TextSqlValue text => ParseSqlDecimal(value: text.Value),
        _ => throw new InvalidCastException()};

    private double ToDouble(SqlValue value) =>
        value switch
    {
        BooleanSqlValue boolean => boolean.Value ? 1 : 0,
        IntegerSqlValue integer => integer.Value,
        DecimalSqlValue exact => double.Parse(s: FormatSqlDecimal(sqlDecimal: exact.Value), provider: CultureInfo.InvariantCulture),
        FloatSqlValue approximate => approximate.Value,
        TextSqlValue text => double.Parse(s: text.Value, style: NumberStyles.Float, provider: CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()};

    private string ToText(SqlValue value) =>
        value switch
    {
        BooleanSqlValue boolean => boolean.Value ? "1" : "0",
        IntegerSqlValue integer => integer.Value.ToString(provider: CultureInfo.InvariantCulture),
        DecimalSqlValue exact => FormatSqlDecimal(sqlDecimal: exact.Value),
        FloatSqlValue approximate => approximate.Value.ToString(format: "R", provider: CultureInfo.InvariantCulture),
        TextSqlValue text => text.Value,
        BinarySqlValue binary => "0x" + Convert.ToHexString(bytes: ReadBinarySqlValue(binarySqlValue: binary)
            .Span),
        DateSqlValue date => date.Value.ToString(format: "yyyy-MM-dd", provider: CultureInfo.InvariantCulture),
        TimeSqlValue time => time.Value.ToString(format: "HH:mm:ss.fffffff", provider: CultureInfo.InvariantCulture),
        DateTimeSqlValue dateTime => dateTime.Value.ToString(format: "yyyy-MM-dd HH:mm:ss.fffffff", provider: CultureInfo.InvariantCulture),
        DateTimeOffsetSqlValue offset => offset.Value.ToString(format: "yyyy-MM-dd HH:mm:ss.fffffff zzz", formatProvider: CultureInfo.InvariantCulture),
        UniqueIdentifierSqlValue identifier => identifier.Value.ToString(format: "D"),
        HierarchyIdSqlValue hierarchy => FormatSqlHierarchyId(sqlHierarchyId: hierarchy.Value),
        SpatialSqlValue spatial => spatial.Value.WellKnownText,
        VectorSqlValue vector => types.ProcessSqlTypeContextVectorFormat(sqlTypeContext: new SqlTypeContext { VectorSource = ReadVector(vectorSqlValue: vector) })
            .TextResult!,
        _ => throw new InvalidCastException()};

    private SqlValue ToBinaryTarget(SqlType target, SqlValue value)
    {
        if (target.Name == SqlTypeName.HierarchyId)
        {
            return value switch
            {
                TextSqlValue text => CreateHierarchyIdSqlValue(path: text.Value),
                BinarySqlValue binary => CreateHierarchyIdSqlValue(sqlHierarchyId: ReadSqlHierarchyId(value: ReadBinarySqlValue(binarySqlValue: binary)
            .Span)),
                _ => throw new InvalidCastException()};
        }

        if (target.Name is SqlTypeName.Geometry or SqlTypeName.Geography)
        {
            if (value is TextSqlValue text)
            {
                return target.Name == SqlTypeName.Geometry ? CreateGeometrySqlValue(wellKnownText: text.Value) : CreateGeographySqlValue(wellKnownText: text.Value);
            }

            if (value is BinarySqlValue binary)
            {
                return new sql_storage_engine.Models.Rows.SpatialSqlValue
                {
                    Value = DecodeSqlSpatial(value: ReadBinarySqlValue(binarySqlValue: binary)
            .Span, expectedKind: target.Name == SqlTypeName.Geometry ? SqlSpatialKind.Geometry : SqlSpatialKind.Geography),
                    StorageFamily = sql_storage_engine.Rows.SqlStorageFamily.Binary
                };
            }

            throw new InvalidCastException();
        }

        return value switch
        {
            BinarySqlValue binary => binary,
            TextSqlValue text when text.Value.StartsWith(value: "0x", comparisonType: StringComparison.OrdinalIgnoreCase) => CreateBinarySqlValue(value: Convert.FromHexString(s: text.Value[2..])),
            TextSqlValue text => CreateBinarySqlValue(value: types.ProcessSqlTypeContextUtf8(sqlTypeContext: new SqlTypeContext { TextResult = text.Value })
            .BinaryResult!),
            UniqueIdentifierSqlValue identifier => CreateBinarySqlValue(value: identifier.Value.ToByteArray()),
            _ => throw new InvalidCastException()};
    }

    private Guid ToGuid(SqlValue value) =>
        value switch
    {
        UniqueIdentifierSqlValue identifier => identifier.Value,
        TextSqlValue text => Guid.Parse(input: text.Value),
        BinarySqlValue binary when ReadBinarySqlValue(binarySqlValue: binary)
            .Length == 16 => new Guid(b: ReadBinarySqlValue(binarySqlValue: binary)
            .Span),
        _ => throw new InvalidCastException()};

    private DateOnly ToDate(SqlValue value) =>
        value switch
    {
        DateSqlValue date => date.Value,
        DateTimeSqlValue dateTime => DateOnly.FromDateTime(dateTime: dateTime.Value),
        DateTimeOffsetSqlValue offset => DateOnly.FromDateTime(dateTime: offset.Value.DateTime),
        TextSqlValue text => DateOnly.Parse(s: text.Value, provider: CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()};

    private TimeOnly ToTime(SqlValue value) =>
        value switch
    {
        TimeSqlValue time => time.Value,
        DateTimeSqlValue dateTime => TimeOnly.FromDateTime(dateTime: dateTime.Value),
        DateTimeOffsetSqlValue offset => TimeOnly.FromDateTime(dateTime: offset.Value.DateTime),
        TextSqlValue text => TimeOnly.Parse(s: text.Value, provider: CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()};

    private DateTime ToDateTime(SqlValue value) =>
        value switch
    {
        DateTimeSqlValue dateTime => dateTime.Value,
        DateSqlValue date => date.Value.ToDateTime(time: TimeOnly.MinValue),
        TimeSqlValue time => new DateOnly(year: 1900, month: 1, day: 1).ToDateTime(time: time.Value),
        DateTimeOffsetSqlValue offset => offset.Value.DateTime,
        IntegerSqlValue days => new DateTime(year: 1900, month: 1, day: 1).AddDays(value: days.Value),
        TextSqlValue text => DateTime.Parse(s: text.Value, provider: CultureInfo.InvariantCulture, styles: DateTimeStyles.AllowWhiteSpaces),
        _ => throw new InvalidCastException()};

    private DateTimeOffset ToDateTimeOffset(SqlValue value) =>
        value switch
    {
        DateTimeOffsetSqlValue offset => offset.Value,
        DateTimeSqlValue dateTime => new DateTimeOffset(dateTime: dateTime.Value, offset: TimeSpan.Zero),
        TextSqlValue text => DateTimeOffset.Parse(input: text.Value, formatProvider: CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()};

    private SqlValue ToVector(SqlType target, SqlValue value)
    {
        if (value is not TextSqlValue text)
        {
            throw new InvalidCastException();
        }

        var numbers = types.ProcessSqlTypeContextVectorParse(sqlTypeContext: new SqlTypeContext { TextResult = text.Value })
            .VectorResult!;

        return target.VectorBaseType == SqlVectorBaseType.Float16 ? CreateHalfVectorSqlValue(values: numbers.Select(selector: item => (Half)item)) : CreateVectorSqlValue(values: numbers);
    }

    private SqlDecimal ParseFloatingDecimal(double value)
    {
        var text = value.ToString(format: "R", provider: CultureInfo.InvariantCulture);
        var exponentAt = text.IndexOfAny(value0: 'E', value1: 'e');

        if (exponentAt < 0)
        {
            return ParseSqlDecimal(value: text);
        }

        var mantissa = ParseSqlDecimal(value: text[..exponentAt]);
        var exponent = int.Parse(s: text[(exponentAt + 1)..], provider: CultureInfo.InvariantCulture);
        var scale = mantissa.Scale - exponent;
        return scale >= 0 ? CreateSqlDecimal(coefficient: mantissa.Coefficient, scale: checked((byte)scale)) : CreateSqlDecimal(coefficient: mantissa.Coefficient * BigInteger.Pow(value: 10, exponent: -scale), scale: 0);
    }

    private SqlType InferType(SqlValue value) =>
        value switch
    {
        BooleanSqlValue => ReadSqlTypeBit(),
        IntegerSqlValue => ReadSqlTypeBigInt(),
        DecimalSqlValue exact => CreateDecimalSqlType(precision: 38, scale: exact.Value.Scale),
        FloatSqlValue => CreateFloatSqlType(),
        TextSqlValue text when text.Value.Length <= 4_000 => CreateNVarCharSqlType(length: Math.Max(val1: 1, val2: text.Value.Length)),
        BinarySqlValue binary when ReadBinarySqlValue(binarySqlValue: binary)
            .Length <= 8_000 => CreateVarBinarySqlType(length: Math.Max(val1: 1, val2: ReadBinarySqlValue(binarySqlValue: binary)
            .Length)),
        DateSqlValue => ReadSqlTypeDate(),
        TimeSqlValue => CreateTimeSqlType(),
        DateTimeSqlValue => CreateDateTime2SqlType(),
        DateTimeOffsetSqlValue => CreateDateTimeOffsetSqlType(),
        UniqueIdentifierSqlValue => ReadSqlTypeUniqueIdentifier(),
        _ => throw new InvalidCastException(message: "The value cannot be represented by sql_variant.")};

    private SqlStorageFamily ReadSqlTypeStorageFamily(SqlType sqlType) =>
        this.ProcessSqlTypeContextStorageFamily3(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .StorageFamilyResult!;

    private SqlType ReadSqlTypeBit() =>
        this.ProcessSqlTypeContextBit7(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    private SqlType ReadSqlTypeBigInt() =>
        this.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    private SqlType ReadSqlTypeDate() =>
        this.ProcessSqlTypeContextDate15(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    private SqlType ReadSqlTypeUniqueIdentifier() =>
        this.ProcessSqlTypeContextUniqueIdentifier18(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    private SqlType CreateDecimalSqlType(byte precision = 18, byte scale = 0) =>
        this.ProcessSqlTypeContextDecimal52(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision, InputbyteScale = scale })
            .SqlTypeResult!;

    private SqlType CreateFloatSqlType(byte precision = 53) =>
        this.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision })
            .SqlTypeResult!;

    private SqlType CreateTimeSqlType(byte scale = 7) =>
        this.ProcessSqlTypeContextTime55(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    private SqlType CreateDateTime2SqlType(byte scale = 7) =>
        this.ProcessSqlTypeContextDateTime256(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    private SqlType CreateDateTimeOffsetSqlType(byte scale = 7) =>
        this.ProcessSqlTypeContextDateTimeOffset57(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    private SqlType CreateNVarCharSqlType(int length = 1, string? collation = null) =>
        this.ProcessSqlTypeContextNVarChar62(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    private SqlType CreateVarBinarySqlType(int length = 1) =>
        this.ProcessSqlTypeContextVarBinary65(sqlTypeContext: new SqlTypeContext { InputintLength = length })
            .SqlTypeResult!;

    private void CheckSqlTypeValue(SqlType sqlType, SqlValue sqlValue, string columnName) =>
        this.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue, InputstringColumnName = columnName });

    private SqlValue NormalizeSqlTypeValue(SqlType sqlType, SqlValue sqlValue) =>
        this.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    private string FormatSqlType(SqlType sqlType) =>
        this.ProcessSqlTypeContextToString80(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .TextResult!;

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

    private SqlValue CreateUniqueIdentifierSqlValue(Guid value) =>
        new UniqueIdentifierSqlValue
    {
        Value = value,
        StorageFamily = SqlStorageFamily.UniqueIdentifier
    };

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

    private SqlValue CreateVectorSqlValue(IEnumerable<float> values)
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

    private SqlValue CreateHalfVectorSqlValue(IEnumerable<Half> values)
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

    private float[] ReadVector(VectorSqlValue vectorSqlValue)
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

    private Half[] ReadHalfVector(VectorSqlValue vectorSqlValue) =>
        vectorSqlValue.BaseType == SqlVectorBaseType.Float16 ? vectorSqlValue.Content.Select(selector: item => BitConverter.UInt16BitsToHalf(value: item))
            .ToArray() : [];

    private SqlValue CreateVariantSqlValue(SqlType sqlType, SqlValue sqlValue)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlType);
        ArgumentNullException.ThrowIfNull(argument: sqlValue);

        if (sqlValue.IsNull)
        {
            throw new ArgumentException(message: "sql_variant cannot wrap SQL NULL.", paramName: nameof(sqlValue));
        }

        if (!ReadSqlTypeCanBeSqlVariantValue(sqlType: sqlType))
        {
            throw new ArgumentException(message: $"{FormatSqlType(sqlType: sqlType)} cannot be stored in sql_variant.", paramName: nameof(sqlType));
        }

        CheckSqlTypeValue(sqlType: sqlType, sqlValue: sqlValue, columnName: "sql_variant");

        return new VariantSqlValue
        {
            DeclaredType = sqlType,
            StorageFamily = SqlStorageFamily.Variant,
            Value = NormalizeSqlTypeValue(sqlType: sqlType, sqlValue: sqlValue)
        };
    }

    private ReadOnlyMemory<byte> ReadBinarySqlValue(BinarySqlValue binarySqlValue) =>
        binarySqlValue.Content.Select(selector: item => checked((byte)item))
            .ToArray();

    private SqlValue CreateDecimalSqlValue(SqlDecimal sqlDecimal) =>
        new DecimalSqlValue
    {
        Value = sqlDecimal,
        StorageFamily = SqlStorageFamily.Decimal
    };

    private SqlDecimal CreateSqlDecimal(BigInteger coefficient, byte scale) =>
        types.ProcessSqlTypeContextDecimalCreate(sqlTypeContext: new SqlTypeContext { CoefficientSource = coefficient, DecimalScale = scale })
            .DecimalResult!.Value;

    private SqlDecimal ParseSqlDecimal(string value) =>
        types.ProcessSqlTypeContextDecimalParse(sqlTypeContext: new SqlTypeContext { TextResult = value })
            .DecimalResult!.Value;

    private string FormatSqlDecimal(SqlDecimal sqlDecimal) =>
        types.ProcessSqlTypeContextDecimalFormat(sqlTypeContext: new SqlTypeContext { DecimalSource = sqlDecimal })
            .TextResult!;

    private string FormatSqlHierarchyId(SqlHierarchyId sqlHierarchyId) =>
        values.ProcessSqlTypeContextHierarchyFormat(sqlTypeContext: new SqlTypeContext { HierarchySource = sqlHierarchyId })
            .TextResult!;

    private SqlHierarchyId ReadSqlHierarchyId(ReadOnlySpan<byte> value) =>
        values.ProcessSqlTypeContextHierarchyRead(sqlTypeContext: new SqlTypeContext { BinarySource = value.ToArray() })
            .HierarchyResult!;

    private SqlValue CreateHierarchyIdSqlValue(string path) =>
        new HierarchyIdSqlValue
    {
        Value = values.ProcessSqlTypeContextHierarchyParse(sqlTypeContext: new SqlTypeContext { TextResult = path })
            .HierarchyResult!,
        StorageFamily = SqlStorageFamily.Binary
    };

    private SqlValue CreateHierarchyIdSqlValue(SqlHierarchyId sqlHierarchyId) =>
        new HierarchyIdSqlValue
    {
        Value = sqlHierarchyId,
        StorageFamily = SqlStorageFamily.Binary
    };

    private SqlValue CreateGeometrySqlValue(string wellKnownText) =>
        new SpatialSqlValue
    {
        Value = spatial.CreateSqlSpatial(sqlSpatial: new SqlSpatial { Kind = SqlSpatialKind.Geometry, Srid = 0, WellKnownText = wellKnownText }),
        StorageFamily = SqlStorageFamily.Binary
    };

    private SqlValue CreateGeographySqlValue(string wellKnownText) =>
        new SpatialSqlValue
    {
        Value = spatial.CreateSqlSpatial(sqlSpatial: new SqlSpatial { Kind = SqlSpatialKind.Geography, Srid = 4326, WellKnownText = wellKnownText }),
        StorageFamily = SqlStorageFamily.Binary
    };

    private SqlSpatial DecodeSqlSpatial(ReadOnlySpan<byte> value, SqlSpatialKind expectedKind) =>
        spatial.CreateSqlSpatial(sqlSpatial: values.ProcessSqlTypeContextSpatialRead(sqlTypeContext: new SqlTypeContext { BinarySource = value.ToArray(), SqlTypeSource = new SqlType { Name = expectedKind == SqlSpatialKind.Geometry ? SqlTypeName.Geometry : SqlTypeName.Geography } })
            .SpatialResult!);

    private bool ReadSqlTypeCanBeSqlVariantValue(SqlType sqlType) =>
        types.ProcessSqlTypeContextCanBeSqlVariantValue6(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .BooleanResult;
}
using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace sql_storage_engine.Rows;

/// <summary>Deterministic SQL assignment conversions used by defaults, expressions, and executor-facing writes.</summary>
public static class SqlConversion
{
    public static SqlValue ConvertTo(SqlType target, SqlValue value) => ConvertToCore(target, null, value);

    /// <summary>Converts a value while retaining source-declaration rules such as money-to-integer rounding.</summary>
    public static SqlValue ConvertTo(SqlType target, SqlType source, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(value);
        var effectiveSource = source.UserTypeKind == SqlUserTypeKind.Alias ? source.BaseType! : source;
        source.Validate(value, "conversion source");
        return ConvertToCore(target, effectiveSource, source.NormalizeForStorage(value));
    }

    private static SqlValue ConvertToCore(SqlType target, SqlType? source, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(value);
        if (value is DefaultSqlValue) throw new ArgumentException("DEFAULT must be resolved before conversion.", nameof(value));
        if (value.IsNull) return value;
        var effective = target.UserTypeKind == SqlUserTypeKind.Alias ? target.BaseType! : target;
        SqlValue converted;
        try
        {
            converted = value.StorageFamily == effective.StorageFamily && CompatibleRuntime(effective, value)
                ? value : ConvertCore(effective, source, value);
            target.Validate(converted, "assignment");
            return target.NormalizeForStorage(converted);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
        { throw new ArgumentException($"Value cannot be converted to {target}.", nameof(value), exception); }
    }

    private static bool CompatibleRuntime(SqlType target, SqlValue value) => (target.StorageFamily, value) switch
    {
        (SqlStorageFamily.Binary, HierarchyIdSqlValue) => target.Name == SqlTypeName.HierarchyId,
        (SqlStorageFamily.Binary, SpatialSqlValue spatial) => target.Name == SqlTypeName.Geometry && spatial.Value.Kind == SqlSpatialKind.Geometry ||
                                                             target.Name == SqlTypeName.Geography && spatial.Value.Kind == SqlSpatialKind.Geography,
        (SqlStorageFamily.Binary, BinarySqlValue) => target.Name is not (SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography),
        (SqlStorageFamily.Text, TextSqlValue) => true,
        (SqlStorageFamily.Vector, VectorSqlValue vector) => vector.BaseType == target.VectorBaseType,
        (_, VariantSqlValue) => target.Name == SqlTypeName.SqlVariant,
        _ => true
    };

    private static SqlValue ConvertCore(SqlType target, SqlType? source, SqlValue value)
    {
        if (target.Name == SqlTypeName.SqlVariant) return value is VariantSqlValue ? value : SqlValue.Variant(InferType(value), value);
        return target.StorageFamily switch
        {
            SqlStorageFamily.Boolean => SqlValue.Boolean(ToBoolean(value)),
            SqlStorageFamily.Integer => SqlValue.Integer(ToInt64(value, source)),
            SqlStorageFamily.Decimal => SqlValue.Decimal(ToDecimal(value)),
            SqlStorageFamily.Float => SqlValue.Float(ToDouble(value)),
            SqlStorageFamily.Text => SqlValue.Text(ToText(value)),
            SqlStorageFamily.Binary => ToBinaryTarget(target, value),
            SqlStorageFamily.UniqueIdentifier => SqlValue.UniqueIdentifier(ToGuid(value)),
            SqlStorageFamily.Date => SqlValue.Date(ToDate(value)),
            SqlStorageFamily.Time => SqlValue.Time(ToTime(value)),
            SqlStorageFamily.DateTime => SqlValue.DateTime(ToDateTime(value)),
            SqlStorageFamily.DateTimeOffset => SqlValue.DateTimeOffset(ToDateTimeOffset(value)),
            SqlStorageFamily.Vector => ToVector(target, value),
            _ => throw new InvalidCastException()
        };
    }

    private static bool ToBoolean(SqlValue value) => value switch
    {
        BooleanSqlValue boolean => boolean.Value, IntegerSqlValue integer => integer.Value != 0,
        DecimalSqlValue exact => !exact.Value.Coefficient.IsZero, FloatSqlValue approximate => approximate.Value != 0,
        TextSqlValue text when bool.TryParse(text.Value, out var boolean) => boolean,
        TextSqlValue text => SqlDecimal.Parse(text.Value).Coefficient != 0,
        _ => throw new InvalidCastException()
    };
    private static long ToInt64(SqlValue value, SqlType? source) => value switch
    {
        BooleanSqlValue boolean => boolean.Value ? 1 : 0, IntegerSqlValue integer => integer.Value,
        DecimalSqlValue exact when source?.Name is SqlTypeName.Money or SqlTypeName.SmallMoney =>
            RoundExactToInt64(exact.Value),
        DecimalSqlValue exact => checked((long)(exact.Value.Coefficient / BigInteger.Pow(10, exact.Value.Scale))),
        FloatSqlValue approximate => checked((long)Math.Truncate(approximate.Value)),
        DateTimeSqlValue dateTime when source?.Name is SqlTypeName.DateTime or SqlTypeName.SmallDateTime =>
            checked((long)Math.Round((dateTime.Value - new DateTime(1900, 1, 1)).TotalDays,
                MidpointRounding.AwayFromZero)),
        TextSqlValue text => long.Parse(text.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()
    };

    private static long RoundExactToInt64(SqlDecimal value)
    {
        value.RoundToScale(0, out var coefficient);
        return checked((long)coefficient);
    }
    private static SqlDecimal ToDecimal(SqlValue value) => value switch
    {
        BooleanSqlValue boolean => new(boolean.Value ? BigInteger.One : BigInteger.Zero, 0),
        IntegerSqlValue integer => new(integer.Value, 0), DecimalSqlValue exact => exact.Value,
        FloatSqlValue approximate => ParseFloatingDecimal(approximate.Value), TextSqlValue text => SqlDecimal.Parse(text.Value),
        _ => throw new InvalidCastException()
    };
    private static double ToDouble(SqlValue value) => value switch
    {
        BooleanSqlValue boolean => boolean.Value ? 1 : 0, IntegerSqlValue integer => integer.Value,
        DecimalSqlValue exact => double.Parse(exact.Value.ToString(), CultureInfo.InvariantCulture),
        FloatSqlValue approximate => approximate.Value,
        TextSqlValue text => double.Parse(text.Value, NumberStyles.Float, CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException()
    };
    private static string ToText(SqlValue value) => value switch
    {
        BooleanSqlValue boolean => boolean.Value ? "1" : "0", IntegerSqlValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        DecimalSqlValue exact => exact.Value.ToString(), FloatSqlValue approximate => approximate.Value.ToString("R", CultureInfo.InvariantCulture),
        TextSqlValue text => text.Value, BinarySqlValue binary => "0x" + Convert.ToHexString(binary.Value.Span),
        DateSqlValue date => date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeSqlValue time => time.Value.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeSqlValue dateTime => dateTime.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffsetSqlValue offset => offset.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
        UniqueIdentifierSqlValue identifier => identifier.Value.ToString("D"), HierarchyIdSqlValue hierarchy => hierarchy.Value.ToString(),
        SpatialSqlValue spatial => spatial.Value.WellKnownText,
        VectorSqlValue vector => JsonSerializer.Serialize(vector.Values),
        _ => throw new InvalidCastException()
    };
    private static SqlValue ToBinaryTarget(SqlType target, SqlValue value)
    {
        if (target.Name == SqlTypeName.HierarchyId) return value switch
        { TextSqlValue text => SqlValue.HierarchyId(text.Value), BinarySqlValue binary => SqlValue.HierarchyId(SqlHierarchyId.Deserialize(binary.Value.Span)), _ => throw new InvalidCastException() };
        if (target.Name is SqlTypeName.Geometry or SqlTypeName.Geography)
        {
            if (value is TextSqlValue text) return target.Name == SqlTypeName.Geometry ? SqlValue.Geometry(text.Value) : SqlValue.Geography(text.Value);
            if (value is BinarySqlValue binary) return new SpatialSqlValue(SqlSpatial.Deserialize(binary.Value.Span,
                target.Name == SqlTypeName.Geometry ? SqlSpatialKind.Geometry : SqlSpatialKind.Geography));
            throw new InvalidCastException();
        }
        return value switch
        {
            BinarySqlValue binary => binary,
            TextSqlValue text when text.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) => SqlValue.Binary(Convert.FromHexString(text.Value[2..])),
            TextSqlValue text => SqlValue.Binary(System.Text.Encoding.UTF8.GetBytes(text.Value)),
            UniqueIdentifierSqlValue identifier => SqlValue.Binary(identifier.Value.ToByteArray()),
            _ => throw new InvalidCastException()
        };
    }
    private static Guid ToGuid(SqlValue value) => value switch
    { UniqueIdentifierSqlValue identifier => identifier.Value, TextSqlValue text => Guid.Parse(text.Value), BinarySqlValue binary when binary.Value.Length == 16 => new Guid(binary.Value.Span), _ => throw new InvalidCastException() };
    private static DateOnly ToDate(SqlValue value) => value switch
    { DateSqlValue date => date.Value, DateTimeSqlValue dateTime => DateOnly.FromDateTime(dateTime.Value), DateTimeOffsetSqlValue offset => DateOnly.FromDateTime(offset.Value.DateTime), TextSqlValue text => DateOnly.Parse(text.Value, CultureInfo.InvariantCulture), _ => throw new InvalidCastException() };
    private static TimeOnly ToTime(SqlValue value) => value switch
    { TimeSqlValue time => time.Value, DateTimeSqlValue dateTime => TimeOnly.FromDateTime(dateTime.Value), DateTimeOffsetSqlValue offset => TimeOnly.FromDateTime(offset.Value.DateTime), TextSqlValue text => TimeOnly.Parse(text.Value, CultureInfo.InvariantCulture), _ => throw new InvalidCastException() };
    private static DateTime ToDateTime(SqlValue value) => value switch
    {
        DateTimeSqlValue dateTime => dateTime.Value, DateSqlValue date => date.Value.ToDateTime(TimeOnly.MinValue),
        TimeSqlValue time => new DateOnly(1900, 1, 1).ToDateTime(time.Value), DateTimeOffsetSqlValue offset => offset.Value.DateTime,
        IntegerSqlValue days => new DateTime(1900, 1, 1).AddDays(days.Value),
        TextSqlValue text => DateTime.Parse(text.Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces), _ => throw new InvalidCastException()
    };
    private static DateTimeOffset ToDateTimeOffset(SqlValue value) => value switch
    { DateTimeOffsetSqlValue offset => offset.Value, DateTimeSqlValue dateTime => new DateTimeOffset(dateTime.Value, TimeSpan.Zero), TextSqlValue text => DateTimeOffset.Parse(text.Value, CultureInfo.InvariantCulture), _ => throw new InvalidCastException() };
    private static SqlValue ToVector(SqlType target, SqlValue value)
    {
        if (value is not TextSqlValue text) throw new InvalidCastException();
        using var document = JsonDocument.Parse(text.Value); if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException();
        return target.VectorBaseType == SqlVectorBaseType.Float16
            ? SqlValue.HalfVector(document.RootElement.EnumerateArray().Select(item => (Half)item.GetSingle()))
            : SqlValue.Vector(document.RootElement.EnumerateArray().Select(item => item.GetSingle()));
    }
    private static SqlDecimal ParseFloatingDecimal(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture); var exponentAt = text.IndexOfAny('E', 'e');
        if (exponentAt < 0) return SqlDecimal.Parse(text);
        var mantissa = SqlDecimal.Parse(text[..exponentAt]); var exponent = int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var scale = mantissa.Scale - exponent;
        return scale >= 0 ? new SqlDecimal(mantissa.Coefficient, checked((byte)scale)) :
            new SqlDecimal(mantissa.Coefficient * BigInteger.Pow(10, -scale), 0);
    }
    private static SqlType InferType(SqlValue value) => value switch
    {
        BooleanSqlValue => SqlType.Bit, IntegerSqlValue => SqlType.BigInt,
        DecimalSqlValue exact => SqlType.Decimal(38, exact.Value.Scale), FloatSqlValue => SqlType.Float(),
        TextSqlValue text when text.Value.Length <= 4_000 => SqlType.NVarChar(Math.Max(1, text.Value.Length)),
        BinarySqlValue binary when binary.Value.Length <= 8_000 => SqlType.VarBinary(Math.Max(1, binary.Value.Length)),
        DateSqlValue => SqlType.Date, TimeSqlValue => SqlType.Time(), DateTimeSqlValue => SqlType.DateTime2(),
        DateTimeOffsetSqlValue => SqlType.DateTimeOffset(), UniqueIdentifierSqlValue => SqlType.UniqueIdentifier,
        _ => throw new InvalidCastException("The value cannot be represented by sql_variant.")
    };
}

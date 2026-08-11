namespace sql_storage_engine.Rows;

public readonly record struct ColumnId(ulong Value)
{
    public override string ToString() => $"column:{Value}";
}

public enum SqlComparison
{
    Less,
    Equal,
    Greater,
    Unknown
}

/// <summary>A closed, typed SQL value model with an explicit NULL representation.</summary>
public abstract record SqlValue
{
    public abstract SqlStorageFamily? StorageFamily { get; }
    public virtual bool IsNull => false;

    public static SqlValue Null { get; } = new NullSqlValue();
    public static SqlValue Default { get; } = new DefaultSqlValue();
    public static SqlValue Boolean(bool value) => new BooleanSqlValue(value);
    public static SqlValue Integer(long value) => new IntegerSqlValue(value);
    public static SqlValue Text(string value) => new TextSqlValue(value ?? throw new ArgumentNullException(nameof(value)));
    public static SqlValue Binary(ReadOnlySpan<byte> value) => new BinarySqlValue(value);
    public static SqlValue Decimal(decimal value) => new DecimalSqlValue(SqlDecimal.FromDecimal(value));
    public static SqlValue Decimal(SqlDecimal value) => new DecimalSqlValue(value);
    public static SqlValue Decimal(System.Numerics.BigInteger coefficient, byte scale) =>
        new DecimalSqlValue(new SqlDecimal(coefficient, scale));
    public static SqlValue Float(double value) => new FloatSqlValue(value);
    public static SqlValue Date(DateOnly value) => new DateSqlValue(value);
    public static SqlValue Time(TimeOnly value) => new TimeSqlValue(value);
    public static SqlValue DateTime(System.DateTime value) => new DateTimeSqlValue(value);
    public static SqlValue DateTimeOffset(System.DateTimeOffset value) => new DateTimeOffsetSqlValue(value);
    public static SqlValue UniqueIdentifier(Guid value) => new UniqueIdentifierSqlValue(value);
    public static SqlValue Variant(SqlType declaredType, SqlValue value) => new VariantSqlValue(declaredType, value);
    public static SqlValue Vector(IEnumerable<float> values) => new VectorSqlValue(values, SqlVectorBaseType.Float32);
    public static SqlValue HalfVector(IEnumerable<Half> values) => new VectorSqlValue(values);
    public static SqlValue HierarchyId(string path) => new HierarchyIdSqlValue(SqlHierarchyId.Parse(path));
    public static SqlValue HierarchyId(SqlHierarchyId value) => new HierarchyIdSqlValue(value ?? throw new ArgumentNullException(nameof(value)));
    public static SqlValue Geometry(string wellKnownText, int srid = 0) => new SpatialSqlValue(new SqlSpatial(SqlSpatialKind.Geometry, srid, wellKnownText));
    public static SqlValue Geography(string wellKnownText, int srid = 4326) => new SpatialSqlValue(new SqlSpatial(SqlSpatialKind.Geography, srid, wellKnownText));
    public static SqlValue Cursor(IReadOnlyList<Row> rows) => new CursorSqlValue(rows ?? throw new ArgumentNullException(nameof(rows)));
    public static SqlValue Table(TableDefinition definition, IEnumerable<Row> rows) => new TableSqlValue(definition, rows);
    public static SqlValue Table(Catalog.CatalogTableType definition, IEnumerable<Row> rows) => new TableSqlValue(definition, rows);

    /// <summary>Converts only supported runtime representations; arbitrary objects are rejected.</summary>
    public static SqlValue From(object? value) => value switch
    {
        null => Null,
        bool boolean => Boolean(boolean),
        byte integer => Integer(integer),
        sbyte integer => Integer(integer),
        short integer => Integer(integer),
        ushort integer => Integer(integer),
        int integer => Integer(integer),
        uint integer => Integer(integer),
        long integer => Integer(integer),
        ulong integer when integer <= long.MaxValue => Integer((long)integer),
        string text => Text(text),
        byte[] bytes => Binary(bytes),
        ReadOnlyMemory<byte> bytes => Binary(bytes.Span),
        decimal exact => Decimal(exact),
        double approximate => Float(approximate),
        float approximate => Float(approximate),
        DateOnly date => Date(date),
        TimeOnly time => Time(time),
        System.DateTime dateTime => DateTime(dateTime),
        System.DateTimeOffset dateTimeOffset => DateTimeOffset(dateTimeOffset),
        Guid identifier => UniqueIdentifier(identifier),
        _ => throw new ArgumentException($"Unsupported SQL runtime representation '{value.GetType().FullName}'.", nameof(value))
    };

    public static SqlComparison Compare(SqlValue left, SqlValue right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.IsNull || right.IsNull) return SqlComparison.Unknown;
        if (left.StorageFamily != right.StorageFamily)
            throw new ArgumentException("SQL values of different storage families cannot be compared.");
        var comparison = (left, right) switch
        {
            (BooleanSqlValue a, BooleanSqlValue b) => a.Value.CompareTo(b.Value),
            (IntegerSqlValue a, IntegerSqlValue b) => a.Value.CompareTo(b.Value),
            (TextSqlValue a, TextSqlValue b) => string.CompareOrdinal(a.Value, b.Value),
            (BinarySqlValue a, BinarySqlValue b) => a.Value.Span.SequenceCompareTo(b.Value.Span),
            (HierarchyIdSqlValue a, HierarchyIdSqlValue b) => a.Value.CompareTo(b.Value),
            (DecimalSqlValue a, DecimalSqlValue b) => a.Value.CompareTo(b.Value),
            (FloatSqlValue a, FloatSqlValue b) => a.Value.CompareTo(b.Value),
            (DateSqlValue a, DateSqlValue b) => a.Value.CompareTo(b.Value),
            (TimeSqlValue a, TimeSqlValue b) => a.Value.CompareTo(b.Value),
            (DateTimeSqlValue a, DateTimeSqlValue b) => a.Value.CompareTo(b.Value),
            (DateTimeOffsetSqlValue a, DateTimeOffsetSqlValue b) => a.Value.CompareTo(b.Value),
            (UniqueIdentifierSqlValue a, UniqueIdentifierSqlValue b) =>
                new System.Data.SqlTypes.SqlGuid(a.Value).CompareTo(new System.Data.SqlTypes.SqlGuid(b.Value)),
            (VariantSqlValue a, VariantSqlValue b) => CompareVariants(a, b),
            _ => throw new InvalidOperationException("Unknown SQL value representation.")
        };
        return comparison switch { < 0 => SqlComparison.Less, > 0 => SqlComparison.Greater, _ => SqlComparison.Equal };
    }

    private static int CompareVariants(VariantSqlValue left, VariantSqlValue right)
    {
        var familyOrder = GetVariantFamilyOrder(left.DeclaredType.Name)
            .CompareTo(GetVariantFamilyOrder(right.DeclaredType.Name));
        if (familyOrder != 0) return familyOrder;
        return GetVariantFamilyOrder(left.DeclaredType.Name) switch
        {
            1 => new System.Data.SqlTypes.SqlGuid(((UniqueIdentifierSqlValue)left.Value).Value).CompareTo(
                new System.Data.SqlTypes.SqlGuid(((UniqueIdentifierSqlValue)right.Value).Value)),
            2 => ((BinarySqlValue)left.Value).Value.Span.SequenceCompareTo(((BinarySqlValue)right.Value).Value.Span),
            3 => CompareVariantText(left, right),
            4 => ToVariantDecimal(left.Value).CompareTo(ToVariantDecimal(right.Value)),
            5 => ((FloatSqlValue)left.Value).Value.CompareTo(((FloatSqlValue)right.Value).Value),
            6 => CompareVariantTemporal(left, right),
            _ => throw new InvalidOperationException("Unknown sql_variant family.")
        };
    }

    private static int GetVariantFamilyOrder(SqlTypeName type) => type switch
    {
        SqlTypeName.UniqueIdentifier => 1,
        SqlTypeName.VarBinary or SqlTypeName.Binary => 2,
        SqlTypeName.NVarChar or SqlTypeName.NChar or SqlTypeName.VarChar or SqlTypeName.Char => 3,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney or
            SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or SqlTypeName.Bit => 4,
        SqlTypeName.Float or SqlTypeName.Real => 5,
        SqlTypeName.DateTimeOffset or SqlTypeName.DateTime2 or SqlTypeName.DateTime or SqlTypeName.SmallDateTime or
            SqlTypeName.Date or SqlTypeName.Time => 6,
        _ => throw new InvalidOperationException($"{type} is not valid in sql_variant.")
    };

    private static SqlDecimal ToVariantDecimal(SqlValue value) => value switch
    {
        BooleanSqlValue boolean => new SqlDecimal(boolean.Value ? 1 : 0, 0),
        IntegerSqlValue integer => new SqlDecimal(integer.Value, 0),
        DecimalSqlValue exact => exact.Value,
        _ => throw new InvalidOperationException("Invalid exact-numeric sql_variant value.")
    };

    private static int CompareVariantTemporal(VariantSqlValue left, VariantSqlValue right)
    {
        var target = TemporalRank(left.DeclaredType.Name) <= TemporalRank(right.DeclaredType.Name)
            ? left.DeclaredType.Name : right.DeclaredType.Name;
        return ToVariantTemporalTicks(left.Value, target).CompareTo(ToVariantTemporalTicks(right.Value, target));
    }

    private static int TemporalRank(SqlTypeName type) => type switch
    {
        SqlTypeName.DateTime2 => 0, SqlTypeName.DateTimeOffset => 1, SqlTypeName.DateTime => 2,
        SqlTypeName.SmallDateTime => 3, SqlTypeName.Date => 4, SqlTypeName.Time => 5,
        _ => throw new InvalidOperationException("Invalid temporal sql_variant type.")
    };

    private static long ToVariantTemporalTicks(SqlValue value, SqlTypeName target) => value switch
    {
        DateSqlValue date => date.Value.ToDateTime(TimeOnly.MinValue).Ticks,
        TimeSqlValue time => new DateTime(1900, 1, 1).Ticks + time.Value.Ticks,
        DateTimeSqlValue dateTime => dateTime.Value.Ticks,
        DateTimeOffsetSqlValue offset when target == SqlTypeName.DateTime2 => offset.Value.DateTime.Ticks,
        DateTimeOffsetSqlValue offset => offset.Value.UtcTicks,
        _ => throw new InvalidOperationException("Invalid temporal sql_variant value.")
    };

    private static int CompareVariantText(VariantSqlValue left, VariantSqlValue right)
    {
        var leftCollation = left.DeclaredType.CollationMetadata ?? SqlCollation.Default;
        var rightCollation = right.DeclaredType.CollationMetadata ?? SqlCollation.Default;
        var collationComparison = leftCollation.CompareMetadataTo(rightCollation);
        if (collationComparison != 0) return collationComparison;
        var leftKey = leftCollation.GetSortKey(((TextSqlValue)left.Value).Value,
            left.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar);
        var rightKey = rightCollation.GetSortKey(((TextSqlValue)right.Value).Value,
            right.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar);
        return leftKey.AsSpan().SequenceCompareTo(rightKey);
    }
}

public sealed record NullSqlValue : SqlValue
{
    internal NullSqlValue() { }
    public override SqlStorageFamily? StorageFamily => null;
    public override bool IsNull => true;
}

/// <summary>Insert-only marker requesting a column default or generated value.</summary>
public sealed record DefaultSqlValue : SqlValue
{
    internal DefaultSqlValue() { }
    public override SqlStorageFamily? StorageFamily => null;
}

public sealed record BooleanSqlValue(bool Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Boolean;
}

public sealed record IntegerSqlValue(long Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Integer;
}

public sealed record TextSqlValue(string Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Text;
}

public sealed record BinarySqlValue : SqlValue
{
    private readonly byte[] _value;
    internal BinarySqlValue(ReadOnlySpan<byte> value) => _value = value.ToArray();
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Binary;
    public ReadOnlyMemory<byte> Value => _value.ToArray();
    public bool Equals(BinarySqlValue? other) => other is not null && _value.AsSpan().SequenceEqual(other._value);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _value) hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record DecimalSqlValue(SqlDecimal Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Decimal;
}

public sealed record FloatSqlValue : SqlValue
{
    public FloatSqlValue(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "SQL floating-point values must be finite.");
        Value = value == 0d ? 0d : value; // Give -0 and +0 one persistent and indexed representation.
    }

    public double Value { get; }
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Float;
}

public sealed record DateSqlValue(DateOnly Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Date;
}

public sealed record TimeSqlValue(TimeOnly Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Time;
}

public sealed record DateTimeSqlValue : SqlValue
{
    public DateTimeSqlValue(System.DateTime value) => Value = new System.DateTime(value.Ticks, DateTimeKind.Unspecified);
    public System.DateTime Value { get; }
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.DateTime;
}

public sealed record DateTimeOffsetSqlValue(System.DateTimeOffset Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.DateTimeOffset;
}

public sealed record UniqueIdentifierSqlValue(Guid Value) : SqlValue
{
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.UniqueIdentifier;
}

public sealed record VariantSqlValue : SqlValue
{
    public VariantSqlValue(SqlType declaredType, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        ArgumentNullException.ThrowIfNull(value);
        if (value.IsNull) throw new ArgumentException("sql_variant cannot wrap SQL NULL.", nameof(value));
        if (!declaredType.CanBeSqlVariantValue)
            throw new ArgumentException($"{declaredType} cannot be stored in sql_variant.", nameof(declaredType));
        declaredType.Validate(value, "sql_variant");
        DeclaredType = declaredType;
        Value = declaredType.NormalizeForStorage(value);
    }

    public SqlType DeclaredType { get; }
    public SqlValue Value { get; }
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Variant;
}

public sealed record VectorSqlValue : SqlValue
{
    private readonly float[]? _floatValues;
    private readonly Half[]? _halfValues;

    internal VectorSqlValue(IEnumerable<float> values, SqlVectorBaseType baseType)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (baseType != SqlVectorBaseType.Float32) throw new ArgumentOutOfRangeException(nameof(baseType));
        _floatValues = values.ToArray();
        if (_floatValues.Any(value => !float.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(values), "SQL vector elements must be finite.");
        BaseType = baseType;
    }

    internal VectorSqlValue(IEnumerable<Half> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _halfValues = values.ToArray();
        if (_halfValues.Any(value => !Half.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(values), "SQL vector elements must be finite.");
        BaseType = SqlVectorBaseType.Float16;
    }

    public SqlVectorBaseType BaseType { get; }
    public IReadOnlyList<float> Values => BaseType == SqlVectorBaseType.Float32
        ? Array.AsReadOnly(_floatValues!)
        : Array.AsReadOnly(_halfValues!.Select(value => (float)value).ToArray());
    internal IReadOnlyList<Half> HalfValues => Array.AsReadOnly(_halfValues ?? []);
    public override SqlStorageFamily? StorageFamily => SqlStorageFamily.Vector;

    public double DistanceTo(VectorSqlValue other, Catalog.SqlVectorDistanceMetric metric = Catalog.SqlVectorDistanceMetric.Cosine)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Enum.IsDefined(metric)) throw new ArgumentOutOfRangeException(nameof(metric));
        if (Values.Count != other.Values.Count) throw new ArgumentException("Vector dimensions must match.", nameof(other));
        double dot = 0, leftSquared = 0, rightSquared = 0, squaredDistance = 0;
        for (var index = 0; index < Values.Count; index++)
        {
            var left = Values[index]; var right = other.Values[index];
            dot += left * right; leftSquared += left * left; rightSquared += right * right;
            var difference = left - right; squaredDistance += difference * difference;
        }
        return metric switch
        {
            Catalog.SqlVectorDistanceMetric.DotProduct => -dot,
            Catalog.SqlVectorDistanceMetric.Euclidean => Math.Sqrt(squaredDistance),
            Catalog.SqlVectorDistanceMetric.Cosine when leftSquared == 0 || rightSquared == 0 => double.NaN,
            Catalog.SqlVectorDistanceMetric.Cosine => 1d - dot / Math.Sqrt(leftSquared * rightSquared),
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
    }

    public bool Equals(VectorSqlValue? other) => other is not null && BaseType == other.BaseType &&
        (BaseType == SqlVectorBaseType.Float32
            ? _floatValues!.AsSpan().SequenceEqual(other._floatValues)
            : _halfValues!.AsSpan().SequenceEqual(other._halfValues));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BaseType);
        foreach (var value in Values) hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record ColumnDefinition
{
    public ColumnDefinition(ColumnId id, string name, SqlType type, bool isNullable,
        Catalog.CatalogColumnEncryption? encryption = null, bool isSparse = false, bool isColumnSet = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(type);
        if (!type.CanBeColumn) throw new ArgumentException($"{type.Name} cannot be used as a table column.", nameof(type));
        Id = id;
        Name = name;
        Type = type;
        IsNullable = isNullable;
        Encryption = encryption;
        IsSparse = isSparse;
        IsColumnSet = isColumnSet;
        if (isSparse && !isNullable) throw new ArgumentException("SPARSE columns must be nullable.", nameof(isSparse));
        if (isColumnSet && (!isNullable || type.Name != SqlTypeName.Xml))
            throw new ArgumentException("A column set must be nullable XML.", nameof(isColumnSet));
    }

    public ColumnId Id { get; }
    public string Name { get; }
    public SqlType Type { get; }
    public bool IsNullable { get; }
    public Catalog.CatalogColumnEncryption? Encryption { get; }
    public bool IsSparse { get; }
    public bool IsColumnSet { get; }

    public void Validate(SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IsNull)
        {
            if (!IsNullable) throw new ArgumentException($"Column '{Name}' is not nullable.", nameof(value));
            return;
        }
        if (IsColumnSet) throw new ArgumentException($"Column set '{Name}' is a virtual projection.", nameof(value));
        Type.Validate(value, Name);
    }
}

public sealed class TableDefinition
{
    private readonly ColumnDefinition[] _columns;

    public TableDefinition(IEnumerable<ColumnDefinition> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        if (_columns.Length == 0) throw new ArgumentException("A table must define at least one column.", nameof(columns));
        if (_columns.Select(column => column.Id).Distinct().Count() != _columns.Length)
            throw new ArgumentException("Column IDs must be unique.", nameof(columns));
        if (_columns.Select(column => column.Name).Distinct(StringComparer.Ordinal).Count() != _columns.Length)
            throw new ArgumentException("Column names must be ordinally unique.", nameof(columns));
    }

    public IReadOnlyList<ColumnDefinition> Columns => Array.AsReadOnly(_columns);

    public void ValidateRow(Row row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Values.Count != _columns.Length)
            throw new ArgumentException($"Expected {_columns.Length} values, received {row.Values.Count}.", nameof(row));
        for (var index = 0; index < _columns.Length; index++) _columns[index].Validate(row.Values[index]);
    }
}

public sealed class Row
{
    private readonly SqlValue[] _values;
    public Row(IEnumerable<SqlValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
        if (_values.Any(value => value is null)) throw new ArgumentException("Rows cannot contain null CLR references.", nameof(values));
    }
    public IReadOnlyList<SqlValue> Values => Array.AsReadOnly(_values);
}

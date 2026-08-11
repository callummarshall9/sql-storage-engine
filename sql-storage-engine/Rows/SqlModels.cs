namespace sql_storage_engine.Rows;

public readonly record struct ColumnId(ulong Value)
{
    public override string ToString() => $"column:{Value}";
}

public enum SqlType : byte
{
    Boolean = 1,
    Integer = 2,
    Text = 3,
    Binary = 4,
    Decimal = 5,
    Float = 6,
    Date = 7,
    Time = 8,
    DateTime = 9,
    DateTimeOffset = 10,
    UniqueIdentifier = 11
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
    public abstract SqlType? Type { get; }
    public virtual bool IsNull => false;

    public static SqlValue Null { get; } = new NullSqlValue();
    public static SqlValue Boolean(bool value) => new BooleanSqlValue(value);
    public static SqlValue Integer(long value) => new IntegerSqlValue(value);
    public static SqlValue Text(string value) => new TextSqlValue(value ?? throw new ArgumentNullException(nameof(value)));
    public static SqlValue Binary(ReadOnlySpan<byte> value) => new BinarySqlValue(value);
    public static SqlValue Decimal(decimal value) => new DecimalSqlValue(value);
    public static SqlValue Float(double value) => new FloatSqlValue(value);
    public static SqlValue Date(DateOnly value) => new DateSqlValue(value);
    public static SqlValue Time(TimeOnly value) => new TimeSqlValue(value);
    public static SqlValue DateTime(System.DateTime value) => new DateTimeSqlValue(value);
    public static SqlValue DateTimeOffset(System.DateTimeOffset value) => new DateTimeOffsetSqlValue(value);
    public static SqlValue UniqueIdentifier(Guid value) => new UniqueIdentifierSqlValue(value);

    /// <summary>Converts only supported runtime representations; arbitrary objects are rejected.</summary>
    public static SqlValue From(object? value) => value switch
    {
        null => Null,
        bool boolean => Boolean(boolean),
        long integer => Integer(integer),
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
        if (left.Type != right.Type) throw new ArgumentException("SQL values of different types cannot be compared.");
        var comparison = (left, right) switch
        {
            (BooleanSqlValue a, BooleanSqlValue b) => a.Value.CompareTo(b.Value),
            (IntegerSqlValue a, IntegerSqlValue b) => a.Value.CompareTo(b.Value),
            (TextSqlValue a, TextSqlValue b) => string.CompareOrdinal(a.Value, b.Value),
            (BinarySqlValue a, BinarySqlValue b) => a.Value.Span.SequenceCompareTo(b.Value.Span),
            (DecimalSqlValue a, DecimalSqlValue b) => a.Value.CompareTo(b.Value),
            (FloatSqlValue a, FloatSqlValue b) => a.Value.CompareTo(b.Value),
            (DateSqlValue a, DateSqlValue b) => a.Value.CompareTo(b.Value),
            (TimeSqlValue a, TimeSqlValue b) => a.Value.CompareTo(b.Value),
            (DateTimeSqlValue a, DateTimeSqlValue b) => a.Value.CompareTo(b.Value),
            (DateTimeOffsetSqlValue a, DateTimeOffsetSqlValue b) => a.Value.CompareTo(b.Value),
            (UniqueIdentifierSqlValue a, UniqueIdentifierSqlValue b) => a.Value.CompareTo(b.Value),
            _ => throw new InvalidOperationException("Unknown SQL value representation.")
        };
        return comparison switch { < 0 => SqlComparison.Less, > 0 => SqlComparison.Greater, _ => SqlComparison.Equal };
    }
}

public sealed record NullSqlValue : SqlValue
{
    internal NullSqlValue() { }
    public override SqlType? Type => null;
    public override bool IsNull => true;
}

public sealed record BooleanSqlValue(bool Value) : SqlValue
{
    public override SqlType? Type => SqlType.Boolean;
}

public sealed record IntegerSqlValue(long Value) : SqlValue
{
    public override SqlType? Type => SqlType.Integer;
}

public sealed record TextSqlValue(string Value) : SqlValue
{
    public override SqlType? Type => SqlType.Text;
}

public sealed record BinarySqlValue : SqlValue
{
    private readonly byte[] _value;
    internal BinarySqlValue(ReadOnlySpan<byte> value) => _value = value.ToArray();
    public override SqlType? Type => SqlType.Binary;
    public ReadOnlyMemory<byte> Value => _value.ToArray();
    public bool Equals(BinarySqlValue? other) => other is not null && _value.AsSpan().SequenceEqual(other._value);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _value) hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record DecimalSqlValue(decimal Value) : SqlValue
{
    public override SqlType? Type => SqlType.Decimal;
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
    public override SqlType? Type => SqlType.Float;
}

public sealed record DateSqlValue(DateOnly Value) : SqlValue
{
    public override SqlType? Type => SqlType.Date;
}

public sealed record TimeSqlValue(TimeOnly Value) : SqlValue
{
    public override SqlType? Type => SqlType.Time;
}

public sealed record DateTimeSqlValue : SqlValue
{
    public DateTimeSqlValue(System.DateTime value) => Value = new System.DateTime(value.Ticks, DateTimeKind.Unspecified);
    public System.DateTime Value { get; }
    public override SqlType? Type => SqlType.DateTime;
}

public sealed record DateTimeOffsetSqlValue(System.DateTimeOffset Value) : SqlValue
{
    public override SqlType? Type => SqlType.DateTimeOffset;
}

public sealed record UniqueIdentifierSqlValue(Guid Value) : SqlValue
{
    public override SqlType? Type => SqlType.UniqueIdentifier;
}

public sealed record ColumnDefinition
{
    public ColumnDefinition(ColumnId id, string name, SqlType type, bool isNullable)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name cannot be empty.", nameof(name));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = id;
        Name = name;
        Type = type;
        IsNullable = isNullable;
    }

    public ColumnId Id { get; }
    public string Name { get; }
    public SqlType Type { get; }
    public bool IsNullable { get; }

    public void Validate(SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IsNull)
        {
            if (!IsNullable) throw new ArgumentException($"Column '{Name}' is not nullable.", nameof(value));
            return;
        }
        if (value.Type != Type)
            throw new ArgumentException($"Column '{Name}' expects {Type}, received {value.Type}.", nameof(value));
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

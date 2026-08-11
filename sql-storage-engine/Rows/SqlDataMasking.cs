using System.Globalization;
using System.Text.RegularExpressions;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Rows;

/// <summary>Applies SQL Server dynamic-data-masking functions for query executors without UNMASK permission.</summary>
public static partial class SqlDataMasking
{
    public static void Validate(SqlType type, string function)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (string.IsNullOrWhiteSpace(function)) throw new ArgumentException("Masking function cannot be empty.", nameof(function));
        function = function.Trim();
        if (function.Equals("default()", StringComparison.OrdinalIgnoreCase)) return;
        if (function.Equals("email()", StringComparison.OrdinalIgnoreCase) || PartialPattern().IsMatch(function))
        {
            if (type.StorageFamily != SqlStorageFamily.Text)
                throw new ArgumentException("Email and partial masks require a character column.", nameof(function));
            return;
        }
        var random = RandomPattern().Match(function);
        if (random.Success)
        {
            if (type.StorageFamily is not (SqlStorageFamily.Integer or SqlStorageFamily.Decimal or SqlStorageFamily.Float))
                throw new ArgumentException("A random mask requires a numeric column.", nameof(function));
            var start = long.Parse(random.Groups["start"].Value, CultureInfo.InvariantCulture);
            var end = long.Parse(random.Groups["end"].Value, CultureInfo.InvariantCulture);
            if (start > end) throw new ArgumentException("A random mask requires an ascending range.", nameof(function));
            return;
        }
        if (DateTimePattern().IsMatch(function))
        {
            if (type.StorageFamily is not (SqlStorageFamily.Date or SqlStorageFamily.Time or
                    SqlStorageFamily.DateTime or SqlStorageFamily.DateTimeOffset))
                throw new ArgumentException("A datetime mask requires a date or time column.", nameof(function));
            return;
        }
        throw new ArgumentException($"Unsupported masking function '{function}'.", nameof(function));
    }

    public static SqlValue Apply(CatalogColumn column, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(column); ArgumentNullException.ThrowIfNull(value);
        if (column.MaskingFunction is null || value.IsNull) return value;
        var function = column.MaskingFunction.Trim();
        if (function.Equals("default()", StringComparison.OrdinalIgnoreCase)) return Default(column.Type, value);
        if (function.Equals("email()", StringComparison.OrdinalIgnoreCase))
        { var text = ((TextSqlValue)SqlConversion.ConvertTo(SqlType.NVarCharMax(), value)).Value; return SqlValue.Text((text.Length == 0 ? "" : text[..1]) + "XXX@XXXX.com"); }
        var partial = PartialPattern().Match(function);
        if (partial.Success)
        {
            var text = ((TextSqlValue)SqlConversion.ConvertTo(SqlType.NVarCharMax(), value)).Value;
            var prefix = int.Parse(partial.Groups["prefix"].Value, CultureInfo.InvariantCulture);
            var suffix = int.Parse(partial.Groups["suffix"].Value, CultureInfo.InvariantCulture);
            var prefixLength = Math.Min(prefix, text.Length);
            var suffixLength = Math.Min(suffix, text.Length - prefixLength);
            var quote = partial.Groups["quote"].Value;
            var padding = partial.Groups["padding"].Value.Replace(quote + quote, quote, StringComparison.Ordinal);
            return SqlValue.Text(text[..prefixLength] + padding + text[(text.Length - suffixLength)..]);
        }
        var random = RandomPattern().Match(function);
        if (random.Success)
        {
            var start = long.Parse(random.Groups["start"].Value, CultureInfo.InvariantCulture);
            var end = long.Parse(random.Groups["end"].Value, CultureInfo.InvariantCulture);
            var masked = end == long.MaxValue
                ? Random.Shared.NextInt64(start, end) + Random.Shared.Next(0, 2)
                : Random.Shared.NextInt64(start, end + 1);
            return SqlConversion.ConvertTo(column.Type, SqlValue.Integer(masked));
        }
        var dateTime = DateTimePattern().Match(function);
        if (dateTime.Success) return MaskDateTime(column.Type, value, dateTime.Groups["part"].Value[0]);
        throw new ArgumentException($"Unsupported masking function '{column.MaskingFunction}'.", nameof(column));
    }

    private static SqlValue Default(SqlType type, SqlValue value) => type.StorageFamily switch
    {
        SqlStorageFamily.Text => SqlValue.Text(new string('X', Math.Min(4, type.Length ?? 4))), SqlStorageFamily.Boolean => SqlValue.Boolean(false),
        SqlStorageFamily.Integer => SqlValue.Integer(0), SqlStorageFamily.Decimal => SqlValue.Decimal(0m),
        SqlStorageFamily.Float => SqlValue.Float(0), SqlStorageFamily.Date => SqlValue.Date(new DateOnly(1900, 1, 1)),
        SqlStorageFamily.Time => SqlValue.Time(TimeOnly.MinValue), SqlStorageFamily.DateTime => SqlValue.DateTime(new DateTime(1900, 1, 1)),
        SqlStorageFamily.DateTimeOffset => SqlValue.DateTimeOffset(new DateTimeOffset(new DateTime(1900, 1, 1), TimeSpan.Zero)),
        SqlStorageFamily.Binary => SqlValue.Binary([0]),
        SqlStorageFamily.UniqueIdentifier => SqlValue.UniqueIdentifier(Guid.Empty), _ => SqlValue.Null
    };

    private static SqlValue MaskDateTime(SqlType type, SqlValue value, char part)
    {
        var source = value switch
        {
            DateSqlValue date => date.Value.ToDateTime(TimeOnly.MinValue),
            TimeSqlValue time => new DateTime(1900, 1, 1).Add(time.Value.ToTimeSpan()),
            DateTimeSqlValue dateTime => dateTime.Value,
            DateTimeOffsetSqlValue offset => offset.Value.DateTime,
            _ => throw new ArgumentException("Value is not a date or time value.", nameof(value))
        };
        var year = source.Year; var month = source.Month; var day = source.Day;
        var hour = source.Hour; var minute = source.Minute; var second = source.Second;
        switch (part)
        {
            case 'Y': year = Random.Shared.Next(1, 10_000); day = Math.Min(day, DateTime.DaysInMonth(year, month)); break;
            case 'M': month = Random.Shared.Next(1, 13); day = Math.Min(day, DateTime.DaysInMonth(year, month)); break;
            case 'D': day = Random.Shared.Next(1, DateTime.DaysInMonth(year, month) + 1); break;
            case 'h': hour = Random.Shared.Next(0, 24); break;
            case 'm': minute = Random.Shared.Next(0, 60); break;
            case 's': second = Random.Shared.Next(0, 60); break;
            default: throw new ArgumentException("Unknown datetime mask component.", nameof(part));
        }
        var masked = new DateTime(year, month, day, hour, minute, second, source.Kind).AddTicks(source.Ticks % TimeSpan.TicksPerSecond);
        return type.StorageFamily switch
        {
            SqlStorageFamily.Date => SqlValue.Date(DateOnly.FromDateTime(masked)),
            SqlStorageFamily.Time => SqlValue.Time(TimeOnly.FromDateTime(masked)),
            SqlStorageFamily.DateTime => SqlValue.DateTime(masked),
            SqlStorageFamily.DateTimeOffset => SqlValue.DateTimeOffset(new DateTimeOffset(masked, ((DateTimeOffsetSqlValue)value).Value.Offset)),
            _ => throw new ArgumentException("Type is not a date or time type.", nameof(type))
        };
    }

    [GeneratedRegex("^partial\\(\\s*(?<prefix>\\d+)\\s*,\\s*(?<quote>['\"])(?<padding>.*)\\k<quote>\\s*,\\s*(?<suffix>\\d+)\\s*\\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartialPattern();
    [GeneratedRegex("^random\\(\\s*(?<start>-?\\d+)\\s*,\\s*(?<end>-?\\d+)\\s*\\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RandomPattern();
    [GeneratedRegex("^datetime\\(\\s*[\"'](?<part>[YMDhms])[\"']\\s*\\)$", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimePattern();
}

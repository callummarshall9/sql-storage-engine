// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskValues;
internal sealed partial class SqlMaskValueService : ISqlMaskValueService
{
    public SqlMaskContext PrepareSqlMaskContextDateTime(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidatePrepareSqlMaskContextDateTime(sqlMaskContext: sqlMaskContext);

        var source = sqlMaskContext.Value switch
        {
            DateSqlValue date => date.Value.ToDateTime(time: TimeOnly.MinValue),
            TimeSqlValue time => new DateTime(year: 1900, month: 1, day: 1).Add(value: time.Value.ToTimeSpan()),
            DateTimeSqlValue dateTime => dateTime.Value,
            DateTimeOffsetSqlValue offset => offset.Value.DateTime,
            _ => throw new ArgumentException(message: "Value is not a date or time value.", paramName: "value")};

        return sqlMaskContext with
        {
            DateTime = source
        };
    });

    public SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateApplySqlMaskContext(sqlMaskContext: sqlMaskContext);
        SqlValue masked;

        switch (sqlMaskContext.Kind)
        {
            case "default":
                masked = Default(sqlMaskContext: sqlMaskContext);
                break;
            case "email":
                var email = sqlMaskContext.Text!;
                masked = new TextSqlValue
                {
                    Value = (email.Length == 0 ? "" : email[..1]) + "XXX@XXXX.com",
                    StorageFamily = SqlStorageFamily.Text
                };
                break;
            case "partial":
                var text = sqlMaskContext.Text!;
                var prefixLength = Math.Min(val1: sqlMaskContext.Prefix, val2: text.Length);
                var suffixLength = Math.Min(val1: sqlMaskContext.Suffix, val2: text.Length - prefixLength);
                masked = new TextSqlValue
                {
                    Value = text[..prefixLength] + sqlMaskContext.Padding + text[(text.Length - suffixLength)..],
                    StorageFamily = SqlStorageFamily.Text
                };
                break;
            case "random":
                masked = new IntegerSqlValue
                {
                    Value = sqlMaskContext.Entropy,
                    StorageFamily = SqlStorageFamily.Integer
                };
                break;
            case "datetime":
                masked = MaskDateTime(sqlMaskContext: sqlMaskContext);
                break;
            default:
                throw new ArgumentException(message: $"Unsupported masking function '{sqlMaskContext.Function}'.", paramName: "column");
        }

        return sqlMaskContext with
        {
            Result = masked
        };
    });

    private static SqlValue Default(SqlMaskContext sqlMaskContext) =>
        sqlMaskContext.Family switch
    {
        SqlStorageFamily.Text => new TextSqlValue
        {
            Value = new string (c: 'X', count: Math.Min(val1: 4, val2: sqlMaskContext.Type!.Length ?? 4)),
            StorageFamily = SqlStorageFamily.Text
        },
        SqlStorageFamily.Boolean => new BooleanSqlValue
        {
            Value = false,
            StorageFamily = SqlStorageFamily.Boolean
        },
        SqlStorageFamily.Integer => new IntegerSqlValue
        {
            Value = 0,
            StorageFamily = SqlStorageFamily.Integer
        },
        SqlStorageFamily.Decimal => new DecimalSqlValue
        {
            Value = new SqlDecimal
            {
                Coefficient = System.Numerics.BigInteger.Zero,
                Scale = 0
            },
            StorageFamily = SqlStorageFamily.Decimal
        },
        SqlStorageFamily.Float => new FloatSqlValue
        {
            Value = 0,
            StorageFamily = SqlStorageFamily.Float
        },
        SqlStorageFamily.Date => new DateSqlValue
        {
            Value = new DateOnly(year: 1900, month: 1, day: 1),
            StorageFamily = SqlStorageFamily.Date
        },
        SqlStorageFamily.Time => new TimeSqlValue
        {
            Value = TimeOnly.MinValue,
            StorageFamily = SqlStorageFamily.Time
        },
        SqlStorageFamily.DateTime => new DateTimeSqlValue
        {
            Value = new DateTime(year: 1900, month: 1, day: 1),
            StorageFamily = SqlStorageFamily.DateTime
        },
        SqlStorageFamily.DateTimeOffset => new DateTimeOffsetSqlValue
        {
            Value = new DateTimeOffset(dateTime: new DateTime(year: 1900, month: 1, day: 1), offset: TimeSpan.Zero),
            StorageFamily = SqlStorageFamily.DateTimeOffset
        },
        SqlStorageFamily.Binary => new BinarySqlValue
        {
            Content = "\0",
            StorageFamily = SqlStorageFamily.Binary
        },
        SqlStorageFamily.UniqueIdentifier => new UniqueIdentifierSqlValue
        {
            Value = Guid.Empty,
            StorageFamily = SqlStorageFamily.UniqueIdentifier
        },
        _ => new NullSqlValue
        {
            IsNull = true
        }
    };

    private static SqlValue MaskDateTime(SqlMaskContext sqlMaskContext)
    {
        var source = sqlMaskContext.DateTime;
        var year = source.Year;
        var month = source.Month;
        var day = source.Day;
        var hour = source.Hour;
        var minute = source.Minute;
        var second = source.Second;
        var random = (int)sqlMaskContext.Entropy;

        switch (sqlMaskContext.Part)
        {
            case 'Y':
                year = random;
                day = Math.Min(val1: day, val2: DateTime.DaysInMonth(year: year, month: month));
                break;
            case 'M':
                month = random;
                day = Math.Min(val1: day, val2: DateTime.DaysInMonth(year: year, month: month));
                break;
            case 'D':
                day = random;
                break;
            case 'h':
                hour = random;
                break;
            case 'm':
                minute = random;
                break;
            case 's':
                second = random;
                break;
            default:
                throw new ArgumentException(message: "Unknown datetime mask component.", paramName: "part");
        }

        var masked = new DateTime(year: year, month: month, day: day, hour: hour, minute: minute, second: second, kind: source.Kind).AddTicks(value: source.Ticks % TimeSpan.TicksPerSecond);

        return sqlMaskContext.Family switch
        {
            SqlStorageFamily.Date => new DateSqlValue
            {
                Value = DateOnly.FromDateTime(dateTime: masked),
                StorageFamily = SqlStorageFamily.Date
            },
            SqlStorageFamily.Time => new TimeSqlValue
            {
                Value = TimeOnly.FromDateTime(dateTime: masked),
                StorageFamily = SqlStorageFamily.Time
            },
            SqlStorageFamily.DateTime => new DateTimeSqlValue
            {
                Value = masked,
                StorageFamily = SqlStorageFamily.DateTime
            },
            SqlStorageFamily.DateTimeOffset => new DateTimeOffsetSqlValue
            {
                Value = new DateTimeOffset(dateTime: masked, offset: ((DateTimeOffsetSqlValue)sqlMaskContext.Value!).Value.Offset),
                StorageFamily = SqlStorageFamily.DateTimeOffset
            },
            _ => throw new ArgumentException(message: "Type is not a date or time type.", paramName: "type")};
    }
}
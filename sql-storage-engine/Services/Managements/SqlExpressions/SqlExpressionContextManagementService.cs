// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using System.Collections.Generic;
using System.Globalization;
using sql_storage_engine.Services.Coordinations.SqlTypes;
using sql_storage_engine.Services.Coordinations.SqlExpressions;
using sql_storage_engine.Services.Coordinations.SqlValueComparisons;

namespace sql_storage_engine.Services.Managements.SqlExpressions;
internal sealed partial class SqlExpressionContextManagementService : ISqlExpressionContextManagementService
{
    private readonly SqlTypeContextCoordinationService types;
    private readonly SqlExpressionRuntimeCoordinationService runtime;
    private readonly SqlValueComparisonCoordinationService comparison;
    public SqlExpressionContextManagementService(SqlTypeContextCoordinationService types, SqlExpressionRuntimeCoordinationService runtime, SqlValueComparisonCoordinationService comparison)
    {
        this.types = types;
        this.runtime = runtime;
        this.comparison = comparison;
    }

    internal static SqlExpressionContextManagementService CreateSqlExpressionContextManagementService() =>
        new(types: SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), runtime: SqlExpressionRuntimeCoordinationService.CreateSqlExpressionRuntimeCoordinationService(), comparison: SqlValueComparisonCoordinationService.CreateSqlValueComparisonCoordinationService());

    public SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateExpression(sqlExpressionContext: sqlExpressionContext);

        var context = sqlExpressionContext with
        {
            Columns = sqlExpressionContext.Columns ?? new System.Collections.Generic.Dictionary<string, SqlValue>(comparer: StringComparer.OrdinalIgnoreCase)
        };

        if (runtime.EvaluateSqlExpressionContext(sqlExpressionContext: context)
            .HostHandled)
        {
            return context;
        }

        runtime.TokenizeSqlExpressionContext(sqlExpressionContext: context);
        context.Result = Or(sqlExpressionContext: context);
        runtime.CompleteSqlExpressionContext(sqlExpressionContext: context);
        return context;
    });

    public SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return runtime.RegisterSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    private SqlValue Or(SqlExpressionContext sqlExpressionContext)
    {
        var value = And(sqlExpressionContext: sqlExpressionContext);

        while (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Or))
        {
            value = Logical(left: value, right: And(sqlExpressionContext: sqlExpressionContext), and: false);
        }

        return value;
    }

    private SqlValue And(SqlExpressionContext sqlExpressionContext)
    {
        var value = Comparison(sqlExpressionContext: sqlExpressionContext);

        while (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.And))
        {
            value = Logical(left: value, right: Comparison(sqlExpressionContext: sqlExpressionContext), and: true);
        }

        return value;
    }

    private SqlValue Comparison(SqlExpressionContext sqlExpressionContext)
    {
        var value = SumExpression(sqlExpressionContext: sqlExpressionContext);

        if (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Is))
        {
            var negate = Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Not);
            Require(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Null);

            return new BooleanSqlValue
            {
                Value = negate ? !value.IsNull : value.IsNull,
                StorageFamily = SqlStorageFamily.Boolean
            };
        }

        var operation = sqlExpressionContext.Token.Kind;

        if (operation is not (ExpressionTokenKind.Equal or ExpressionTokenKind.NotEqual or ExpressionTokenKind.Less or ExpressionTokenKind.LessEqual or ExpressionTokenKind.Greater or ExpressionTokenKind.GreaterEqual))
        {
            return value;
        }

        Advance(sqlExpressionContext: sqlExpressionContext);
        var right = SumExpression(sqlExpressionContext: sqlExpressionContext);

        if (value.IsNull || right.IsNull)
        {
            return new NullSqlValue
            {
                IsNull = true
            };
        }

        var comparison = Compare(left: value, right: right);

        return new BooleanSqlValue
        {
            Value = operation switch
            {
                ExpressionTokenKind.Equal => comparison == 0,
                ExpressionTokenKind.NotEqual => comparison != 0,
                ExpressionTokenKind.Less => comparison < 0,
                ExpressionTokenKind.LessEqual => comparison <= 0,
                ExpressionTokenKind.Greater => comparison > 0,
                ExpressionTokenKind.GreaterEqual => comparison >= 0,
                _ => false
            },
            StorageFamily = SqlStorageFamily.Boolean
        };
    }

    private SqlValue SumExpression(SqlExpressionContext sqlExpressionContext)
    {
        var value = Multiplicative(sqlExpressionContext: sqlExpressionContext);

        while (sqlExpressionContext.Token.Kind is ExpressionTokenKind.Plus or ExpressionTokenKind.Minus)
        {
            var operation = sqlExpressionContext.Token.Kind;
            Advance(sqlExpressionContext: sqlExpressionContext);
            value = Arithmetic(left: value, right: Multiplicative(sqlExpressionContext: sqlExpressionContext), operation: operation);
        }

        return value;
    }

    private SqlValue Multiplicative(SqlExpressionContext sqlExpressionContext)
    {
        var value = Unary(sqlExpressionContext: sqlExpressionContext);

        while (sqlExpressionContext.Token.Kind is ExpressionTokenKind.Star or ExpressionTokenKind.Slash or ExpressionTokenKind.Percent)
        {
            var operation = sqlExpressionContext.Token.Kind;
            Advance(sqlExpressionContext: sqlExpressionContext);
            value = Arithmetic(left: value, right: Unary(sqlExpressionContext: sqlExpressionContext), operation: operation);
        }

        return value;
    }

    private SqlValue Unary(SqlExpressionContext sqlExpressionContext)
    {
        if (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Not))
        {
            var value = Unary(sqlExpressionContext: sqlExpressionContext);

            return value.IsNull ? value : new BooleanSqlValue
            {
                Value = !AsBoolean(value: value),
                StorageFamily = SqlStorageFamily.Boolean
            };
        }

        if (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Plus))
        {
            return Unary(sqlExpressionContext: sqlExpressionContext);
        }

        if (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Minus))
        {
            var value = Unary(sqlExpressionContext: sqlExpressionContext);

            return value switch
            {
                IntegerSqlValue integer => new IntegerSqlValue
                {
                    Value = checked(-integer.Value),
                    StorageFamily = SqlStorageFamily.Integer
                },
                DecimalSqlValue exact => new DecimalSqlValue
                {
                    Value = ProjectExact(coefficient: -exact.Value.Coefficient, scale: exact.Value.Scale),
                    StorageFamily = SqlStorageFamily.Decimal
                },
                FloatSqlValue approximate => ProjectFloat(value: -approximate.Value),
                _ => throw Error(message: "Unary minus requires a number.")};
        }

        return Primary(sqlExpressionContext: sqlExpressionContext);
    }

    private SqlValue Primary(SqlExpressionContext sqlExpressionContext)
    {
        if (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.LeftParen))
        {
            var value = Or(sqlExpressionContext: sqlExpressionContext);
            Require(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.RightParen);
            return value;
        }

        if (sqlExpressionContext.Token.Kind == ExpressionTokenKind.Null)
        {
            Advance(sqlExpressionContext: sqlExpressionContext);

            return new NullSqlValue
            {
                IsNull = true
            };
        }

        if (sqlExpressionContext.Token.Kind == ExpressionTokenKind.String)
        {
            var value = new TextSqlValue
            {
                Value = sqlExpressionContext.Token.Text,
                StorageFamily = SqlStorageFamily.Text
            };

            Advance(sqlExpressionContext: sqlExpressionContext);
            return value;
        }

        if (sqlExpressionContext.Token.Kind == ExpressionTokenKind.Hex)
        {
            var value = new BinarySqlValue
            {
                Content = sql_storage_engine.Brokers.BinaryPayloads.BinaryPayloadBroker.ReadBinaryContent(bytes: Convert.FromHexString(s: sqlExpressionContext.Token.Text)),
                StorageFamily = SqlStorageFamily.Binary
            };

            Advance(sqlExpressionContext: sqlExpressionContext);
            return value;
        }

        if (sqlExpressionContext.Token.Kind == ExpressionTokenKind.Number)
        {
            var text = sqlExpressionContext.Token.Text;
            Advance(sqlExpressionContext: sqlExpressionContext);

            return !text.Contains(value: '.') && long.TryParse(s: text, style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out var integer) ? new IntegerSqlValue
            {
                Value = integer,
                StorageFamily = SqlStorageFamily.Integer
            }

            : new DecimalSqlValue
            {
                Value = comparison.ParseSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Text = text })
            .Exact,
                StorageFamily = SqlStorageFamily.Decimal
            };
        }

        if (sqlExpressionContext.Token.Kind != ExpressionTokenKind.Identifier)
        {
            throw Error(message: "Expected a scalar value.");
        }

        var name = sqlExpressionContext.Token.Text;
        Advance(sqlExpressionContext: sqlExpressionContext);

        if (name.Equals(value: "CURRENT_TIMESTAMP", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return new DateTimeSqlValue
            {
                Value = runtime.ReadSqlExpressionContextTimestamp(sqlExpressionContext: new SqlExpressionContext())
            .Timestamp,
                StorageFamily = SqlStorageFamily.DateTime
            };
        }

        if (!Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.LeftParen))
        {
            if (sqlExpressionContext.Columns!.TryGetValue(key: name, value: out var column))
            {
                return column;
            }

            throw Error(message: $"Unknown column or expression name '{name}'.");
        }

        var arguments = new List<SqlValue>();

        if (!Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.RightParen))
        {
            do
            {
                arguments.Add(item: Or(sqlExpressionContext: sqlExpressionContext));
            }
            while (Take(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.Comma));

            Require(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.RightParen);
        }

        return Function(name: name, arguments: arguments);
    }

    private SqlValue Function(string name, IReadOnlyList<SqlValue> arguments)
    {
        if (name.Equals(value: "NEWID", comparisonType: StringComparison.OrdinalIgnoreCase) || name.Equals(value: "NEWSEQUENTIALID", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 0);

            return new UniqueIdentifierSqlValue
            {
                Value = runtime.CreateSqlExpressionContextIdentifier(sqlExpressionContext: new SqlExpressionContext())
            .Identifier,
                StorageFamily = SqlStorageFamily.UniqueIdentifier
            };
        }

        if (name.Equals(value: "GETDATE", comparisonType: StringComparison.OrdinalIgnoreCase) || name.Equals(value: "SYSDATETIME", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 0);

            return new DateTimeSqlValue
            {
                Value = runtime.ReadSqlExpressionContextTimestamp(sqlExpressionContext: new SqlExpressionContext())
            .Timestamp,
                StorageFamily = SqlStorageFamily.DateTime
            };
        }

        if (name.Equals(value: "GETUTCDATE", comparisonType: StringComparison.OrdinalIgnoreCase) || name.Equals(value: "SYSUTCDATETIME", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 0);

            return new DateTimeSqlValue
            {
                Value = runtime.ReadSqlExpressionContextTimestamp(sqlExpressionContext: new SqlExpressionContext { Utc = true })
            .Timestamp,
                StorageFamily = SqlStorageFamily.DateTime
            };
        }

        if (name.Equals(value: "ISNULL", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 2);
            return arguments[0].IsNull ? arguments[1] : arguments[0];
        }

        if (name.Equals(value: "COALESCE", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return arguments.FirstOrDefault(predicate: value => !value.IsNull) ?? new NullSqlValue
            {
                IsNull = true
            };
        }

        if (name.Equals(value: "LOWER", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 1);

            return arguments[0].IsNull ? arguments[0] : new TextSqlValue
            {
                Value = ((TextSqlValue)arguments[0]).Value.ToLowerInvariant(),
                StorageFamily = SqlStorageFamily.Text
            };
        }

        if (name.Equals(value: "UPPER", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 1);

            return arguments[0].IsNull ? arguments[0] : new TextSqlValue
            {
                Value = ((TextSqlValue)arguments[0]).Value.ToUpperInvariant(),
                StorageFamily = SqlStorageFamily.Text
            };
        }

        if (name.Equals(value: "LEN", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 1);

            return arguments[0].IsNull ? arguments[0] : new IntegerSqlValue
            {
                Value = ((TextSqlValue)arguments[0]).Value.TrimEnd()
            .Length,
                StorageFamily = SqlStorageFamily.Integer
            };
        }

        if (name.Equals(value: "ABS", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            Arity(values: arguments, count: 1);

            return arguments[0] switch
            {
                IntegerSqlValue integer => new IntegerSqlValue
                {
                    Value = Math.Abs(value: integer.Value),
                    StorageFamily = SqlStorageFamily.Integer
                },
                DecimalSqlValue exact => new DecimalSqlValue
                {
                    Value = ProjectExact(coefficient: System.Numerics.BigInteger.Abs(value: exact.Value.Coefficient), scale: exact.Value.Scale),
                    StorageFamily = SqlStorageFamily.Decimal
                },
                FloatSqlValue approximate => ProjectFloat(value: Math.Abs(value: approximate.Value)),
                _ => throw new ArgumentException(message: "ABS requires a number.")};
        }

        if (name.Equals(value: "CONCAT", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return new TextSqlValue
            {
                Value = string.Concat(values: arguments.Select(selector: value => value.IsNull ? "" : ((TextSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: value)).Value)),
                StorageFamily = SqlStorageFamily.Text
            };
        }

        throw new ArgumentException(message: $"Unsupported persisted SQL function '{name}'. Register {nameof(ISqlExpressionRuntime)} for additional functions.");
    }

    private SqlValue Arithmetic(SqlValue left, SqlValue right, ExpressionTokenKind operation)
    {
        if (left.IsNull || right.IsNull)
        {
            return new NullSqlValue
            {
                IsNull = true
            };
        }

        if (operation == ExpressionTokenKind.Plus && left is TextSqlValue leftText && right is TextSqlValue rightText)
        {
            return new TextSqlValue
            {
                Value = leftText.Value + rightText.Value,
                StorageFamily = SqlStorageFamily.Text
            };
        }

        if (left is FloatSqlValue || right is FloatSqlValue)
        {
            var a = ((FloatSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: left)).Value;

            var b = ((FloatSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: right)).Value;

            return ProjectFloat(value: operation switch
            {
                ExpressionTokenKind.Plus => a + b,
                ExpressionTokenKind.Minus => a - b,
                ExpressionTokenKind.Star => a * b,
                ExpressionTokenKind.Slash => a / b,
                ExpressionTokenKind.Percent => a % b,
                _ => throw new InvalidOperationException()});
        }

        if (left is DecimalSqlValue || right is DecimalSqlValue)
        {
            return DecimalArithmetic(left: ToExact(value: left), right: ToExact(value: right), operation: operation);
        }

        var x = ((IntegerSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: left)).Value;

        var y = ((IntegerSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: right)).Value;

        return new IntegerSqlValue
        {
            Value = operation switch
            {
                ExpressionTokenKind.Plus => checked(x + y),
                ExpressionTokenKind.Minus => checked(x - y),
                ExpressionTokenKind.Star => checked(x * y),
                ExpressionTokenKind.Slash => x / y,
                ExpressionTokenKind.Percent => x % y,
                _ => throw new InvalidOperationException()},
            StorageFamily = SqlStorageFamily.Integer
        };
    }

    private SqlDecimal ToExact(SqlValue value) =>
        comparison.PrepareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = value })
            .Exact;

    private SqlValue DecimalArithmetic(SqlDecimal left, SqlDecimal right, ExpressionTokenKind operation) =>
        comparison.CalculateSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = new DecimalSqlValue { Value = left, StorageFamily = SqlStorageFamily.Decimal }, Right = new DecimalSqlValue { Value = right, StorageFamily = SqlStorageFamily.Decimal }, Operator = operation })
            .Result!;

    private int Compare(SqlValue left, SqlValue right)
    {
        if (left.StorageFamily != right.StorageFamily)
        {
            if (left is FloatSqlValue || right is FloatSqlValue)
            {
                var a = ((FloatSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: left)).Value;

                var b = ((FloatSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: right)).Value;

                return a.CompareTo(value: b);
            }

            if ((left is IntegerSqlValue or DecimalSqlValue) && (right is IntegerSqlValue or DecimalSqlValue))
            {
                return comparison.CompareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = new DecimalSqlValue { Value = ToExact(value: left), StorageFamily = SqlStorageFamily.Decimal }, Right = new DecimalSqlValue { Value = ToExact(value: right), StorageFamily = SqlStorageFamily.Decimal } })
            .Order;
            }

            right = ConvertValue(sqlType: InferComparableType(value: left), sqlValue: right);
        }

        return comparison.CompareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = left, Right = right })
            .Comparison switch
        {
            SqlComparison.Less => -1,
            SqlComparison.Greater => 1,
            _ => 0
        };
    }

    private SqlType InferComparableType(SqlValue value) =>
        value switch
    {
        TextSqlValue => types.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        IntegerSqlValue => types.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        DecimalSqlValue => types.ProcessSqlTypeContextDecimal52(sqlTypeContext: new SqlTypeContext { InputbytePrecision = 38, InputbyteScale = 18 })
            .SqlTypeResult!,
        FloatSqlValue => types.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        DateSqlValue => types.ProcessSqlTypeContextDate15(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        TimeSqlValue => types.ProcessSqlTypeContextTime55(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        DateTimeSqlValue => types.ProcessSqlTypeContextDateTime256(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        DateTimeOffsetSqlValue => types.ProcessSqlTypeContextDateTimeOffset57(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        UniqueIdentifierSqlValue => types.ProcessSqlTypeContextUniqueIdentifier18(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!,
        _ => throw new ArgumentException(message: "Values cannot be compared.")};

    private SqlValue Logical(SqlValue left, SqlValue right, bool and)
    {
        if (!left.IsNull && AsBoolean(value: left) != and || !right.IsNull && AsBoolean(value: right) != and)
        {
            return new BooleanSqlValue
            {
                Value = !and,
                StorageFamily = SqlStorageFamily.Boolean
            };
        }

        return left.IsNull || right.IsNull ? new NullSqlValue
        {
            IsNull = true
        }

        : new BooleanSqlValue
        {
            Value = and,
            StorageFamily = SqlStorageFamily.Boolean
        };
    }

    private bool AsBoolean(SqlValue value) =>
        ((BooleanSqlValue)ConvertValue(sqlType: types.ProcessSqlTypeContextBit7(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!, sqlValue: value)).Value;

    private static void Arity(IReadOnlyCollection<SqlValue> values, int count)
    {
        if (values.Count != count)
        {
            throw new ArgumentException(message: $"Function requires {count} arguments.");
        }
    }

    private bool Take(SqlExpressionContext sqlExpressionContext, ExpressionTokenKind kind)
    {
        sqlExpressionContext.Expected = kind;

        return runtime.TakeSqlExpressionContext(sqlExpressionContext: sqlExpressionContext)
            .Consumed;
    }

    private void Require(SqlExpressionContext sqlExpressionContext, ExpressionTokenKind kind)
    {
        sqlExpressionContext.Expected = kind;
        runtime.RequireSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    }

    private void Advance(SqlExpressionContext sqlExpressionContext) =>
        runtime.AdvanceSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);

    private static FormatException Error(string message) =>
        new(message: message);

    private SqlValue ConvertValue(SqlType sqlType, SqlValue sqlValue) =>
        types.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    private static SqlValue ProjectFloat(double value)
    {
        if (!double.IsFinite(d: value))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(value));
        }

        return new FloatSqlValue
        {
            Value = value == 0 ? 0 : value,
            StorageFamily = SqlStorageFamily.Float
        };
    }

    private SqlDecimal ProjectExact(BigInteger coefficient, byte scale)
    {
        var value = new SqlDecimal
        {
            Coefficient = coefficient,
            Scale = scale
        };

        var result = comparison.PrepareSqlValueComparisonContext(sqlValueComparisonContext: new SqlValueComparisonContext { Left = new DecimalSqlValue { Value = value, StorageFamily = SqlStorageFamily.Decimal } })
            .Exact;

        return result;
    }

    public SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnDefinitions(sqlExpressionContext: sqlExpressionContext);
        return runtime.ReadSqlExpressionContextComputedOrder(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnReference(sqlExpressionContext: sqlExpressionContext);
        return runtime.ReadSqlExpressionContextColumnReference(sqlExpressionContext: sqlExpressionContext);
    });
}
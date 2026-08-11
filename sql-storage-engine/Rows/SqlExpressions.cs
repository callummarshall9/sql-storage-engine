using System.Globalization;
using System.Numerics;

namespace sql_storage_engine.Rows;

/// <summary>Host override for expressions that require a broader Transact-SQL execution environment.</summary>
public interface ISqlExpressionRuntime
{
    SqlValue Evaluate(string expression, IReadOnlyDictionary<string, SqlValue> columns);
}

/// <summary>A deterministic scalar-expression evaluator for persisted defaults, computed columns, and checks.</summary>
public static class SqlExpressions
{
    public static ISqlExpressionRuntime? Runtime { get; set; }

    public static SqlValue Evaluate(string expression, IReadOnlyDictionary<string, SqlValue>? columns = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        columns ??= new Dictionary<string, SqlValue>(StringComparer.OrdinalIgnoreCase);
        if (Runtime is { } runtime) return runtime.Evaluate(expression, columns);
        var parser = new Parser(expression, columns); var value = parser.Expression(); parser.RequireEnd(); return value;
    }

    private ref struct Parser
    {
        private Lexer _lexer; private Token _token; private readonly IReadOnlyDictionary<string, SqlValue> _columns;
        public Parser(string source, IReadOnlyDictionary<string, SqlValue> columns)
        { _lexer = new Lexer(source); _columns = columns; _token = _lexer.Next(); }
        public SqlValue Expression() => Or();
        public void RequireEnd() { if (_token.Kind != TokenKind.End) throw Error("Unexpected expression input."); }
        private SqlValue Or()
        {
            var value = And();
            while (Take(TokenKind.Or)) value = Logical(value, And(), false);
            return value;
        }
        private SqlValue And()
        {
            var value = Comparison();
            while (Take(TokenKind.And)) value = Logical(value, Comparison(), true);
            return value;
        }
        private SqlValue Comparison()
        {
            var value = Additive();
            if (Take(TokenKind.Is))
            {
                var negate = Take(TokenKind.Not); Require(TokenKind.Null);
                return SqlValue.Boolean(negate ? !value.IsNull : value.IsNull);
            }
            var operation = _token.Kind;
            if (operation is not (TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual)) return value;
            Advance(); var right = Additive(); if (value.IsNull || right.IsNull) return SqlValue.Null;
            var comparison = Compare(value, right);
            return SqlValue.Boolean(operation switch
            {
                TokenKind.Equal => comparison == 0, TokenKind.NotEqual => comparison != 0,
                TokenKind.Less => comparison < 0, TokenKind.LessEqual => comparison <= 0,
                TokenKind.Greater => comparison > 0, TokenKind.GreaterEqual => comparison >= 0, _ => false
            });
        }
        private SqlValue Additive()
        {
            var value = Multiplicative();
            while (_token.Kind is TokenKind.Plus or TokenKind.Minus)
            { var operation = _token.Kind; Advance(); value = Arithmetic(value, Multiplicative(), operation); }
            return value;
        }
        private SqlValue Multiplicative()
        {
            var value = Unary();
            while (_token.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
            { var operation = _token.Kind; Advance(); value = Arithmetic(value, Unary(), operation); }
            return value;
        }
        private SqlValue Unary()
        {
            if (Take(TokenKind.Not)) { var value = Unary(); return value.IsNull ? value : SqlValue.Boolean(!AsBoolean(value)); }
            if (Take(TokenKind.Plus)) return Unary();
            if (Take(TokenKind.Minus))
            {
                var value = Unary(); return value switch
                { IntegerSqlValue integer => SqlValue.Integer(checked(-integer.Value)), DecimalSqlValue exact => SqlValue.Decimal(new SqlDecimal(-exact.Value.Coefficient, exact.Value.Scale)), FloatSqlValue approximate => SqlValue.Float(-approximate.Value), _ => throw Error("Unary minus requires a number.") };
            }
            return Primary();
        }
        private SqlValue Primary()
        {
            if (Take(TokenKind.LeftParen)) { var value = Expression(); Require(TokenKind.RightParen); return value; }
            if (_token.Kind == TokenKind.Null) { Advance(); return SqlValue.Null; }
            if (_token.Kind == TokenKind.String) { var value = SqlValue.Text(_token.Text); Advance(); return value; }
            if (_token.Kind == TokenKind.Hex) { var value = SqlValue.Binary(Convert.FromHexString(_token.Text)); Advance(); return value; }
            if (_token.Kind == TokenKind.Number)
            {
                var text = _token.Text; Advance();
                return !text.Contains('.') && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    ? SqlValue.Integer(integer) : SqlValue.Decimal(SqlDecimal.Parse(text));
            }
            if (_token.Kind != TokenKind.Identifier) throw Error("Expected a scalar value.");
            var name = _token.Text; Advance();
            if (name.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)) return SqlValue.DateTime(DateTime.Now);
            if (!Take(TokenKind.LeftParen))
            {
                if (_columns.TryGetValue(name, out var column)) return column;
                throw Error($"Unknown column or expression name '{name}'.");
            }
            var arguments = new List<SqlValue>();
            if (!Take(TokenKind.RightParen))
            { do { arguments.Add(Expression()); } while (Take(TokenKind.Comma)); Require(TokenKind.RightParen); }
            return Function(name, arguments);
        }
        private static SqlValue Function(string name, IReadOnlyList<SqlValue> arguments)
        {
            if (name.Equals("NEWID", StringComparison.OrdinalIgnoreCase) || name.Equals("NEWSEQUENTIALID", StringComparison.OrdinalIgnoreCase))
            { Arity(arguments, 0); return SqlValue.UniqueIdentifier(Guid.NewGuid()); }
            if (name.Equals("GETDATE", StringComparison.OrdinalIgnoreCase) || name.Equals("SYSDATETIME", StringComparison.OrdinalIgnoreCase))
            { Arity(arguments, 0); return SqlValue.DateTime(DateTime.Now); }
            if (name.Equals("GETUTCDATE", StringComparison.OrdinalIgnoreCase) || name.Equals("SYSUTCDATETIME", StringComparison.OrdinalIgnoreCase))
            { Arity(arguments, 0); return SqlValue.DateTime(DateTime.UtcNow); }
            if (name.Equals("ISNULL", StringComparison.OrdinalIgnoreCase)) { Arity(arguments, 2); return arguments[0].IsNull ? arguments[1] : arguments[0]; }
            if (name.Equals("COALESCE", StringComparison.OrdinalIgnoreCase)) return arguments.FirstOrDefault(value => !value.IsNull) ?? SqlValue.Null;
            if (name.Equals("LOWER", StringComparison.OrdinalIgnoreCase)) { Arity(arguments, 1); return arguments[0].IsNull ? arguments[0] : SqlValue.Text(((TextSqlValue)arguments[0]).Value.ToLowerInvariant()); }
            if (name.Equals("UPPER", StringComparison.OrdinalIgnoreCase)) { Arity(arguments, 1); return arguments[0].IsNull ? arguments[0] : SqlValue.Text(((TextSqlValue)arguments[0]).Value.ToUpperInvariant()); }
            if (name.Equals("LEN", StringComparison.OrdinalIgnoreCase)) { Arity(arguments, 1); return arguments[0].IsNull ? arguments[0] : SqlValue.Integer(((TextSqlValue)arguments[0]).Value.TrimEnd().Length); }
            if (name.Equals("ABS", StringComparison.OrdinalIgnoreCase))
            { Arity(arguments, 1); return arguments[0] switch { IntegerSqlValue integer => SqlValue.Integer(Math.Abs(integer.Value)), DecimalSqlValue exact => SqlValue.Decimal(new SqlDecimal(System.Numerics.BigInteger.Abs(exact.Value.Coefficient), exact.Value.Scale)), FloatSqlValue approximate => SqlValue.Float(Math.Abs(approximate.Value)), _ => throw new ArgumentException("ABS requires a number.") }; }
            if (name.Equals("CONCAT", StringComparison.OrdinalIgnoreCase)) return SqlValue.Text(string.Concat(arguments.Select(value => value.IsNull ? "" : ((TextSqlValue)SqlConversion.ConvertTo(SqlType.NVarCharMax(), value)).Value)));
            throw new ArgumentException($"Unsupported persisted SQL function '{name}'. Register {nameof(ISqlExpressionRuntime)} for additional functions.");
        }
        private static SqlValue Arithmetic(SqlValue left, SqlValue right, TokenKind operation)
        {
            if (left.IsNull || right.IsNull) return SqlValue.Null;
            if (operation == TokenKind.Plus && left is TextSqlValue leftText && right is TextSqlValue rightText)
                return SqlValue.Text(leftText.Value + rightText.Value);
            if (left is FloatSqlValue || right is FloatSqlValue)
            {
                var a = ((FloatSqlValue)SqlConversion.ConvertTo(SqlType.Float(), left)).Value; var b = ((FloatSqlValue)SqlConversion.ConvertTo(SqlType.Float(), right)).Value;
                return SqlValue.Float(operation switch { TokenKind.Plus => a + b, TokenKind.Minus => a - b, TokenKind.Star => a * b, TokenKind.Slash => a / b, TokenKind.Percent => a % b, _ => throw new InvalidOperationException() });
            }
            if (left is DecimalSqlValue || right is DecimalSqlValue)
                return DecimalArithmetic(ToExact(left), ToExact(right), operation);
            var x = ((IntegerSqlValue)SqlConversion.ConvertTo(SqlType.BigInt, left)).Value;
            var y = ((IntegerSqlValue)SqlConversion.ConvertTo(SqlType.BigInt, right)).Value;
            return SqlValue.Integer(operation switch { TokenKind.Plus => checked(x + y), TokenKind.Minus => checked(x - y), TokenKind.Star => checked(x * y), TokenKind.Slash => x / y, TokenKind.Percent => x % y, _ => throw new InvalidOperationException() });
        }

        private static SqlDecimal ToExact(SqlValue value) => value switch
        {
            DecimalSqlValue exact => exact.Value,
            IntegerSqlValue integer => new SqlDecimal(integer.Value, 0),
            BooleanSqlValue boolean => new SqlDecimal(boolean.Value ? 1 : 0, 0),
            TextSqlValue text => SqlDecimal.Parse(text.Value),
            _ => throw new ArgumentException("Value cannot participate in exact numeric arithmetic.")
        };

        private static SqlValue DecimalArithmetic(SqlDecimal left, SqlDecimal right, TokenKind operation)
        {
            var p1 = DecimalPrecision(left); var s1 = (int)left.Scale;
            var p2 = DecimalPrecision(right); var s2 = (int)right.Scale;
            int precision; int scale;
            BigInteger coefficient;
            switch (operation)
            {
                case TokenKind.Plus:
                case TokenKind.Minus:
                    scale = Math.Max(s1, s2);
                    precision = scale + Math.Max(p1 - s1, p2 - s2) + 1;
                    left.TryRescale(checked((byte)scale), out var addLeft);
                    right.TryRescale(checked((byte)scale), out var addRight);
                    coefficient = operation == TokenKind.Plus ? addLeft + addRight : addLeft - addRight;
                    ReduceAdditive(ref precision, ref scale, Math.Max(p1 - s1, p2 - s2));
                    break;
                case TokenKind.Star:
                    precision = p1 + p2 + 1; scale = s1 + s2;
                    coefficient = left.Coefficient * right.Coefficient;
                    ReduceMultiplicative(ref precision, ref scale);
                    break;
                case TokenKind.Slash:
                    if (right.Coefficient.IsZero) throw new DivideByZeroException();
                    scale = Math.Max(6, s1 + p2 + 1);
                    precision = p1 - s1 + s2 + scale;
                    ReduceMultiplicative(ref precision, ref scale);
                    coefficient = DivideRounded(left.Coefficient, right.Coefficient, s2 + scale - s1);
                    return SqlValue.Decimal(new SqlDecimal(coefficient, checked((byte)scale)));
                case TokenKind.Percent:
                    scale = Math.Max(s1, s2);
                    precision = Math.Min(p1 - s1, p2 - s2) + scale;
                    left.TryRescale(checked((byte)scale), out var modLeft);
                    right.TryRescale(checked((byte)scale), out var modRight);
                    if (modRight.IsZero) throw new DivideByZeroException();
                    coefficient = modLeft % modRight;
                    if (precision > 38) { scale = Math.Max(0, scale - (precision - 38)); precision = 38; }
                    break;
                default: throw new InvalidOperationException();
            }
            var intermediateScale = operation == TokenKind.Star ? s1 + s2 : Math.Max(s1, s2);
            coefficient = RoundCoefficient(coefficient, intermediateScale, scale);
            return SqlValue.Decimal(new SqlDecimal(coefficient, checked((byte)scale)));
        }

        private static int DecimalPrecision(SqlDecimal value) =>
            Math.Max(value.Scale, checked((byte)Math.Max(1, SqlDecimal.CountDigits(BigInteger.Abs(value.Coefficient)))));

        private static void ReduceAdditive(ref int precision, ref int scale, int integralDigits)
        {
            if (precision <= 38) return;
            scale = Math.Max(0, 38 - integralDigits);
            precision = 38;
        }

        private static void ReduceMultiplicative(ref int precision, ref int scale)
        {
            if (precision <= 38) return;
            var integralDigits = precision - scale;
            if (integralDigits < 32) scale = Math.Min(scale, 38 - integralDigits);
            else if (scale > 6) scale = 6;
            precision = 38;
        }

        private static BigInteger DivideRounded(BigInteger numerator, BigInteger denominator, int decimalShift)
        {
            if (decimalShift >= 0) numerator *= BigInteger.Pow(10, decimalShift);
            else denominator *= BigInteger.Pow(10, -decimalShift);
            var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
            if (BigInteger.Abs(remainder) * 2 >= BigInteger.Abs(denominator))
                quotient += numerator.Sign * denominator.Sign;
            return quotient;
        }

        private static BigInteger RoundCoefficient(BigInteger coefficient, int sourceScale, int targetScale)
        {
            if (targetScale >= sourceScale) return coefficient * BigInteger.Pow(10, targetScale - sourceScale);
            var divisor = BigInteger.Pow(10, sourceScale - targetScale);
            var quotient = BigInteger.DivRem(coefficient, divisor, out var remainder);
            if (BigInteger.Abs(remainder) * 2 >= divisor) quotient += coefficient.Sign;
            return quotient;
        }
        private static int Compare(SqlValue left, SqlValue right)
        {
            if (left.StorageFamily != right.StorageFamily)
            {
                if (left is FloatSqlValue || right is FloatSqlValue)
                {
                    var a = ((FloatSqlValue)SqlConversion.ConvertTo(SqlType.Float(), left)).Value;
                    var b = ((FloatSqlValue)SqlConversion.ConvertTo(SqlType.Float(), right)).Value;
                    return a.CompareTo(b);
                }
                if ((left is IntegerSqlValue or DecimalSqlValue) && (right is IntegerSqlValue or DecimalSqlValue))
                    return ToExact(left).CompareTo(ToExact(right));
                right = SqlConversion.ConvertTo(InferComparableType(left), right);
            }
            return SqlValue.Compare(left, right) switch { SqlComparison.Less => -1, SqlComparison.Greater => 1, _ => 0 };
        }
        private static SqlType InferComparableType(SqlValue value) => value switch
        { TextSqlValue => SqlType.NVarCharMax(), IntegerSqlValue => SqlType.BigInt, DecimalSqlValue => SqlType.Decimal(38, 18), FloatSqlValue => SqlType.Float(), DateSqlValue => SqlType.Date, TimeSqlValue => SqlType.Time(), DateTimeSqlValue => SqlType.DateTime2(), DateTimeOffsetSqlValue => SqlType.DateTimeOffset(), UniqueIdentifierSqlValue => SqlType.UniqueIdentifier, _ => throw new ArgumentException("Values cannot be compared.") };
        private static SqlValue Logical(SqlValue left, SqlValue right, bool and)
        {
            if (and)
            { if (!left.IsNull && !AsBoolean(left) || !right.IsNull && !AsBoolean(right)) return SqlValue.Boolean(false); if (left.IsNull || right.IsNull) return SqlValue.Null; return SqlValue.Boolean(true); }
            if (!left.IsNull && AsBoolean(left) || !right.IsNull && AsBoolean(right)) return SqlValue.Boolean(true);
            return left.IsNull || right.IsNull ? SqlValue.Null : SqlValue.Boolean(false);
        }
        private static bool AsBoolean(SqlValue value) => ((BooleanSqlValue)SqlConversion.ConvertTo(SqlType.Bit, value)).Value;
        private static void Arity(IReadOnlyCollection<SqlValue> values, int count) { if (values.Count != count) throw new ArgumentException($"Function requires {count} arguments."); }
        private bool Take(TokenKind kind) { if (_token.Kind != kind) return false; Advance(); return true; }
        private void Require(TokenKind kind) { if (!Take(kind)) throw Error($"Expected {kind}."); }
        private void Advance() => _token = _lexer.Next();
        private static FormatException Error(string message) => new(message);
    }

    private ref struct Lexer
    {
        private readonly ReadOnlySpan<char> _source; private int _position;
        public Lexer(string source) { _source = source.AsSpan(); _position = 0; }
        public Token Next()
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position])) _position++;
            if (_position == _source.Length) return new(TokenKind.End, "");
            var ch = _source[_position++];
            if (ch == '[') { var end = _source[_position..].IndexOf(']'); if (end < 0) throw new FormatException("Unclosed SQL identifier."); var text = _source.Slice(_position, end).ToString(); _position += end + 1; return new(TokenKind.Identifier, text); }
            if (ch == '\'' || (ch is 'N' or 'n') && _position < _source.Length && _source[_position] == '\'')
            { if (ch != '\'') _position++; var text = new System.Text.StringBuilder(); while (_position < _source.Length) { ch = _source[_position++]; if (ch != '\'') { text.Append(ch); continue; } if (_position < _source.Length && _source[_position] == '\'') { _position++; text.Append('\''); continue; } return new(TokenKind.String, text.ToString()); } throw new FormatException("Unclosed SQL string."); }
            if (ch == '0' && _position < _source.Length && _source[_position] is 'x' or 'X')
            { _position++; var start = _position; while (_position < _source.Length && Uri.IsHexDigit(_source[_position])) _position++; var text = _source[start.._position].ToString(); if (text.Length % 2 != 0) throw new FormatException("Binary literal requires complete bytes."); return new(TokenKind.Hex, text); }
            if (char.IsDigit(ch)) { var start = _position - 1; while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '.')) _position++; return new(TokenKind.Number, _source[start.._position].ToString()); }
            if (char.IsLetter(ch) || ch is '_' or '@')
            { var start = _position - 1; while (_position < _source.Length && (char.IsLetterOrDigit(_source[_position]) || _source[_position] is '_' or '@' or '$')) _position++; var text = _source[start.._position].ToString(); return new(Keyword(text), text); }
            return ch switch
            {
                '(' => new(TokenKind.LeftParen, "("), ')' => new(TokenKind.RightParen, ")"), ',' => new(TokenKind.Comma, ","),
                '+' => new(TokenKind.Plus, "+"), '-' => new(TokenKind.Minus, "-"), '*' => new(TokenKind.Star, "*"), '/' => new(TokenKind.Slash, "/"), '%' => new(TokenKind.Percent, "%"),
                '=' => new(TokenKind.Equal, "="), '<' when Peek('=') => Consume(TokenKind.LessEqual, "<="), '<' when Peek('>') => Consume(TokenKind.NotEqual, "<>"),
                '<' => new(TokenKind.Less, "<"), '>' when Peek('=') => Consume(TokenKind.GreaterEqual, ">="), '>' => new(TokenKind.Greater, ">"),
                '!' when Peek('=') => Consume(TokenKind.NotEqual, "!="), _ => throw new FormatException($"Unexpected SQL expression character '{ch}'.")
            };
        }
        private bool Peek(char value) => _position < _source.Length && _source[_position] == value;
        private Token Consume(TokenKind kind, string text) { _position++; return new(kind, text); }
        private static TokenKind Keyword(string text) => text.ToUpperInvariant() switch
        { "AND" => TokenKind.And, "OR" => TokenKind.Or, "NOT" => TokenKind.Not, "IS" => TokenKind.Is, "NULL" => TokenKind.Null, _ => TokenKind.Identifier };
    }
    private readonly record struct Token(TokenKind Kind, string Text);
    private enum TokenKind : byte
    { End, Identifier, Number, String, Hex, LeftParen, RightParen, Comma, Plus, Minus, Star, Slash, Percent, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, And, Or, Not, Is, Null }
}

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal sealed partial class SqlExpressionTokenProcessingService : ISqlExpressionTokenProcessingService
{
    public SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        var tokens = new System.Collections.Generic.List<SqlExpressionToken>();
        sqlExpressionContext.Position = 0;

        while (true)
        {
            var token = Next(sqlExpressionContext: sqlExpressionContext);
            tokens.Add(item: token);

            if (token.Kind == ExpressionTokenKind.End)
            {
                break;
            }
        }

        sqlExpressionContext.Tokens = tokens.ToArray();
        sqlExpressionContext.Position = 0;
        sqlExpressionContext.Token = sqlExpressionContext.Tokens[0];
        return sqlExpressionContext;
    });

    private static SqlExpressionToken Next(SqlExpressionContext sqlExpressionContext)
    {
        while (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && char.IsWhiteSpace(c: sqlExpressionContext.Source![sqlExpressionContext.Position]))
        {
            sqlExpressionContext.Position++;
        }

        if (sqlExpressionContext.Position == sqlExpressionContext.Source!.Length)
        {
            return new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.End,
                Text = ""
            };
        }

        var ch = sqlExpressionContext.Source![sqlExpressionContext.Position++];

        if (ch == '[')
        {
            var end = sqlExpressionContext.Source!.AsSpan(start: sqlExpressionContext.Position)
            .IndexOf(value: ']');

            if (end < 0)
            {
                throw new FormatException(message: "Unclosed SQL identifier.");
            }

            var text = sqlExpressionContext.Source!.AsSpan(start: sqlExpressionContext.Position, length: end)
            .ToString();

            sqlExpressionContext.Position += end + 1;

            return new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Identifier,
                Text = text
            };
        }

        if (ch == '\'' || (ch is 'N' or 'n') && sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && sqlExpressionContext.Source![sqlExpressionContext.Position] == '\'')
        {
            if (ch != '\'')
            {
                sqlExpressionContext.Position++;
            }

            var text = new System.Text.StringBuilder();

            while (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length)
            {
                ch = sqlExpressionContext.Source![sqlExpressionContext.Position++];

                if (ch != '\'')
                {
                    text.Append(value: ch);
                    continue;
                }

                if (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && sqlExpressionContext.Source![sqlExpressionContext.Position] == '\'')
                {
                    sqlExpressionContext.Position++;
                    text.Append(value: '\'');
                    continue;
                }

                return new SqlExpressionToken
                {
                    Kind = ExpressionTokenKind.String,
                    Text = text.ToString()
                };
            }

            throw new FormatException(message: "Unclosed SQL string.");
        }

        if (ch == '0' && sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && sqlExpressionContext.Source![sqlExpressionContext.Position] is 'x' or 'X')
        {
            sqlExpressionContext.Position++;
            var start = sqlExpressionContext.Position;

            while (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && Uri.IsHexDigit(character: sqlExpressionContext.Source![sqlExpressionContext.Position]))
            {
                sqlExpressionContext.Position++;
            }

            var text = sqlExpressionContext.Source![start..sqlExpressionContext.Position].ToString();

            if (text.Length % 2 != 0)
            {
                throw new FormatException(message: "Binary literal requires complete bytes.");
            }

            return new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Hex,
                Text = text
            };
        }

        if (char.IsDigit(c: ch))
        {
            var start = sqlExpressionContext.Position - 1;

            while (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && (char.IsDigit(c: sqlExpressionContext.Source![sqlExpressionContext.Position]) || sqlExpressionContext.Source![sqlExpressionContext.Position] == '.'))
            {
                sqlExpressionContext.Position++;
            }

            return new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Number,
                Text = sqlExpressionContext.Source![start..sqlExpressionContext.Position].ToString()
            };
        }

        if (char.IsLetter(c: ch) || ch is '_' or '@')
        {
            var start = sqlExpressionContext.Position - 1;

            while (sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && (char.IsLetterOrDigit(c: sqlExpressionContext.Source![sqlExpressionContext.Position]) || sqlExpressionContext.Source![sqlExpressionContext.Position] is '_' or '@' or '$'))
            {
                sqlExpressionContext.Position++;
            }

            var text = sqlExpressionContext.Source![start..sqlExpressionContext.Position].ToString();

            return new SqlExpressionToken
            {
                Kind = Keyword(text: text),
                Text = text
            };
        }

        return ch switch
        {
            '(' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.LeftParen,
                Text = "("
            },
            ')' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.RightParen,
                Text = ")"
            },
            ',' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Comma,
                Text = ","
            },
            '+' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Plus,
                Text = "+"
            },
            '-' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Minus,
                Text = "-"
            },
            '*' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Star,
                Text = "*"
            },
            '/' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Slash,
                Text = "/"
            },
            '%' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Percent,
                Text = "%"
            },
            '=' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Equal,
                Text = "="
            },
            '<' when Peek(sqlExpressionContext: sqlExpressionContext, value: '=') => Consume(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.LessEqual, text: "<="),
            '<' when Peek(sqlExpressionContext: sqlExpressionContext, value: '>') => Consume(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.NotEqual, text: "<>"),
            '<' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Less,
                Text = "<"
            },
            '>' when Peek(sqlExpressionContext: sqlExpressionContext, value: '=') => Consume(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.GreaterEqual, text: ">="),
            '>' => new SqlExpressionToken
            {
                Kind = ExpressionTokenKind.Greater,
                Text = ">"
            },
            '!' when Peek(sqlExpressionContext: sqlExpressionContext, value: '=') => Consume(sqlExpressionContext: sqlExpressionContext, kind: ExpressionTokenKind.NotEqual, text: "!="),
            _ => throw new FormatException(message: $"Unexpected SQL expression character '{ch}'.")};
    }

    private static bool Peek(SqlExpressionContext sqlExpressionContext, char value) =>
        sqlExpressionContext.Position < sqlExpressionContext.Source!.Length && sqlExpressionContext.Source![sqlExpressionContext.Position] == value;

    private static SqlExpressionToken Consume(SqlExpressionContext sqlExpressionContext, ExpressionTokenKind kind, string text)
    {
        sqlExpressionContext.Position++;

        return new SqlExpressionToken
        {
            Kind = kind,
            Text = text
        };
    }

    private static ExpressionTokenKind Keyword(string text) =>
        text.ToUpperInvariant() switch
    {
        "AND" => ExpressionTokenKind.And,
        "OR" => ExpressionTokenKind.Or,
        "NOT" => ExpressionTokenKind.Not,
        "IS" => ExpressionTokenKind.Is,
        "NULL" => ExpressionTokenKind.Null,
        _ => ExpressionTokenKind.Identifier
    };
}
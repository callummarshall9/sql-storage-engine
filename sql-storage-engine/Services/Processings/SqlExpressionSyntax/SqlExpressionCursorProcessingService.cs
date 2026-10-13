// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal sealed partial class SqlExpressionCursorProcessingService : ISqlExpressionCursorProcessingService
{
    public SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        Advance(sqlExpressionContext: sqlExpressionContext);
        return sqlExpressionContext;
    });

    public SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        sqlExpressionContext.Consumed = sqlExpressionContext.Token.Kind == sqlExpressionContext.Expected;

        if (sqlExpressionContext.Consumed)
        {
            Advance(sqlExpressionContext: sqlExpressionContext);
        }

        return sqlExpressionContext;
    });

    public SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);

        if (!Take(sqlExpressionContext: sqlExpressionContext)
            .Consumed)
        {
            throw new FormatException(message: $"Expected {sqlExpressionContext.Expected}.");
        }

        return sqlExpressionContext;
    });

    public SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);

        if (sqlExpressionContext.Token.Kind != ExpressionTokenKind.End)
        {
            throw new FormatException(message: "Unexpected expression input.");
        }

        return sqlExpressionContext;
    });

    private static void Advance(SqlExpressionContext sqlExpressionContext)
    {
        if (sqlExpressionContext.Position + 1 >= sqlExpressionContext.Tokens!.Length)
        {
            throw new FormatException(message: "Unexpected end of SQL expression.");
        }

        sqlExpressionContext.Token = sqlExpressionContext.Tokens[++sqlExpressionContext.Position];
    }

    private static SqlExpressionContext Take(SqlExpressionContext sqlExpressionContext)
    {
        sqlExpressionContext.Consumed = sqlExpressionContext.Token.Kind == sqlExpressionContext.Expected;

        if (sqlExpressionContext.Consumed)
        {
            Advance(sqlExpressionContext: sqlExpressionContext);
        }

        return sqlExpressionContext;
    }
}
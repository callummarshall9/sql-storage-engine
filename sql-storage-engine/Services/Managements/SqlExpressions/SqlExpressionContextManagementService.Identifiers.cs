// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlExpressions;
internal sealed partial class SqlExpressionContextManagementService
{
    public SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateIdentifierRequest(sqlExpressionContext: sqlExpressionContext);
        return runtime.CreateSqlExpressionContextIdentifier(sqlExpressionContext: sqlExpressionContext);
    });

    private static void ValidateIdentifierRequest(SqlExpressionContext sqlExpressionContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext);
    }
}
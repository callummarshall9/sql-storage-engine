// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Managements.SqlExpressions;

namespace sql_storage_engine.Exposures.Rows;
public sealed class SqlExpressionExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private SqlExpressionExposure()
    {
    }

    private static readonly SqlExpressionContextManagementService service = SqlExpressionContextManagementService.CreateSqlExpressionContextManagementService();
    public static SqlValue Evaluate(string expression, IReadOnlyDictionary<string, SqlValue>? columns = null) =>
        service.EvaluateSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Source = expression, Columns = columns })
            .Result!;

    public static void RegisterRuntime(ISqlExpressionRuntime runtime) =>
        service.RegisterSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Runtime = (source, columns) => runtime.Evaluate(expression: source, columns: (IReadOnlyDictionary<string, SqlValue>)columns) });

    public static void ClearRuntime() =>
        service.RegisterSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext());
}
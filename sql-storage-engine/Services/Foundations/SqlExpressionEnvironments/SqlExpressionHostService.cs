// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionHostService : ISqlExpressionHostService
{
    private readonly sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionHostBroker broker;
    public SqlExpressionHostService(sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionHostBroker broker)
    {
        this.broker = broker;
    }

    internal static SqlExpressionHostService CreateSqlExpressionHostService() =>
        new(broker: sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionHostBroker.CreateSqlExpressionHostBroker());

    public SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        broker.Register(runtime: sqlExpressionContext.Runtime);
        return sqlExpressionContext;
    });

    public SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        ArgumentNullException.ThrowIfNull(argument: sqlExpressionContext.Columns);
        var result = broker.Evaluate(source: sqlExpressionContext.Source!, columns: sqlExpressionContext.Columns);
        sqlExpressionContext.HostHandled = result is not null;
        sqlExpressionContext.Result = (SqlValue? )result;
        return sqlExpressionContext;
    });
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionClockService : ISqlExpressionClockService
{
    private readonly sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionClockBroker broker;
    public SqlExpressionClockService(sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionClockBroker broker)
    {
        this.broker = broker;
    }

    internal static SqlExpressionClockService CreateSqlExpressionClockService() =>
        new(broker: sql_storage_engine.Brokers.SqlExpressionEnvironments.SqlExpressionClockBroker.CreateSqlExpressionClockBroker());

    public SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        sqlExpressionContext.Timestamp = broker.ReadTimestamp(utc: sqlExpressionContext.Utc, timeProviderSource: sqlExpressionContext.TimeProviderSource);
        return sqlExpressionContext;
    });

    public SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        sqlExpressionContext.Identifier = broker.CreateIdentifier();
        return sqlExpressionContext;
    });
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;

namespace sql_storage_engine.Services.Orchestrations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionEnvironmentOrchestrationService : ISqlExpressionEnvironmentOrchestrationService
{
    private readonly SqlExpressionClockService clock;
    private readonly SqlExpressionHostService host;
    public SqlExpressionEnvironmentOrchestrationService(SqlExpressionClockService clock, SqlExpressionHostService host)
    {
        this.clock = clock;
        this.host = host;
    }

    internal static SqlExpressionEnvironmentOrchestrationService CreateSqlExpressionEnvironmentOrchestrationService() =>
        new(clock: SqlExpressionClockService.CreateSqlExpressionClockService(), host: SqlExpressionHostService.CreateSqlExpressionHostService());

    public SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return clock.ReadSqlExpressionContextTimestamp(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return clock.CreateSqlExpressionContextIdentifier(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return host.RegisterSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        return host.EvaluateSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });
}
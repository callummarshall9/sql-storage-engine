// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Orchestrations.SqlExpressionSyntax;
using sql_storage_engine.Services.Orchestrations.SqlExpressionEnvironments;

namespace sql_storage_engine.Services.Coordinations.SqlExpressions;
internal sealed partial class SqlExpressionRuntimeCoordinationService : ISqlExpressionRuntimeCoordinationService
{
    private readonly SqlExpressionSyntaxOrchestrationService syntax;
    private readonly SqlExpressionEnvironmentOrchestrationService environment;
    public SqlExpressionRuntimeCoordinationService(SqlExpressionSyntaxOrchestrationService syntax, SqlExpressionEnvironmentOrchestrationService environment)
    {
        this.syntax = syntax;
        this.environment = environment;
    }

    internal static SqlExpressionRuntimeCoordinationService CreateSqlExpressionRuntimeCoordinationService() =>
        new(syntax: SqlExpressionSyntaxOrchestrationService.CreateSqlExpressionSyntaxOrchestrationService(), environment: SqlExpressionEnvironmentOrchestrationService.CreateSqlExpressionEnvironmentOrchestrationService());

    public SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        return syntax.TokenizeSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return syntax.AdvanceSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return syntax.TakeSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return syntax.RequireSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return syntax.CompleteSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return environment.ReadSqlExpressionContextTimestamp(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return environment.CreateSqlExpressionContextIdentifier(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(sqlExpressionContext: sqlExpressionContext);
        return environment.RegisterSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        return environment.EvaluateSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnDefinitions(sqlExpressionContext: sqlExpressionContext);
        return syntax.ReadSqlExpressionContextComputedOrder(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnReference(sqlExpressionContext: sqlExpressionContext);
        return syntax.ReadSqlExpressionContextColumnReference(sqlExpressionContext: sqlExpressionContext);
    });
}
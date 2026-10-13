// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.SqlExpressionSyntax;

namespace sql_storage_engine.Services.Orchestrations.SqlExpressionSyntax;
internal sealed partial class SqlExpressionSyntaxOrchestrationService : ISqlExpressionSyntaxOrchestrationService
{
    private readonly SqlExpressionTokenProcessingService tokens;
    private readonly SqlExpressionCursorProcessingService cursor;
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.CatalogColumnExpressionProcessingService columns;
    public SqlExpressionSyntaxOrchestrationService(SqlExpressionTokenProcessingService tokens, SqlExpressionCursorProcessingService cursor, sql_storage_engine.Services.Processings.CatalogMetadata.CatalogColumnExpressionProcessingService columns)
    {
        this.tokens = tokens;
        this.cursor = cursor;
        this.columns = columns;
    }

    internal static SqlExpressionSyntaxOrchestrationService CreateSqlExpressionSyntaxOrchestrationService() =>
        new(tokens: new SqlExpressionTokenProcessingService(), cursor: new SqlExpressionCursorProcessingService(), columns: new sql_storage_engine.Services.Processings.CatalogMetadata.CatalogColumnExpressionProcessingService());

    public SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(sqlExpressionContext: sqlExpressionContext);
        return tokens.TokenizeSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return cursor.AdvanceSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return cursor.TakeSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return cursor.RequireSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTokens(sqlExpressionContext: sqlExpressionContext);
        return cursor.CompleteSqlExpressionContext(sqlExpressionContext: sqlExpressionContext);
    });

    public SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnDefinitions(sqlExpressionContext: sqlExpressionContext);

        sqlExpressionContext.ComputedColumnOrder = columns.PrepareCatalogColumnExpressionContext(catalogColumnExpressionContext: new sql_storage_engine.Models.Catalog.CatalogColumnExpressionContext { Columns = sqlExpressionContext.ColumnDefinitions, Operation = 2 })
            .ComputedColumnOrder;

        return sqlExpressionContext;
    });

    public SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnReference(sqlExpressionContext: sqlExpressionContext);

        sqlExpressionContext.DirectColumnReference = columns.PrepareCatalogColumnExpressionContext(catalogColumnExpressionContext: new sql_storage_engine.Models.Catalog.CatalogColumnExpressionContext { Expression = sqlExpressionContext.Source, Columns = sqlExpressionContext.ColumnDefinitions, Operation = 3 })
            .DirectColumnReference;

        return sqlExpressionContext;
    });
}
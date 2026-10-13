// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlMaskValues;
internal sealed partial class SqlMaskValueOrchestrationService : ISqlMaskValueOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.SqlMaskValues.SqlMaskValueService values = new sql_storage_engine.Services.Foundations.SqlMaskValues.SqlMaskValueService();
    private readonly sql_storage_engine.Services.Foundations.SqlMaskEntropies.SqlMaskEntropyService entropy = new sql_storage_engine.Services.Foundations.SqlMaskEntropies.SqlMaskEntropyService();
    public SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateApplySqlMaskContext(sqlMaskContext: sqlMaskContext);
        var context = sqlMaskContext;

        if (context.Kind == "datetime")
        {
            context = values.PrepareSqlMaskContextDateTime(sqlMaskContext: context);
        }

        if (context.Kind is "random" or "datetime")
        {
            context = entropy.ReadSqlMaskContext(sqlMaskContext: context);
        }

        return values.ApplySqlMaskContext(sqlMaskContext: context);
    });
}
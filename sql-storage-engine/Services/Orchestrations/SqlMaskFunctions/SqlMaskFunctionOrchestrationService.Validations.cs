// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlMaskFunctions;
internal sealed partial class SqlMaskFunctionOrchestrationService
{
    private static void ValidateReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (string.IsNullOrWhiteSpace(value: sqlMaskContext?.Function), nameof(SqlMaskContext.Function))]);

    private static void ValidateCheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        ValidateReadSqlMaskContext(sqlMaskContext: sqlMaskContext);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Valid masking inputs are required.", paramName: input.ParameterName);
            }
        }
    }
}
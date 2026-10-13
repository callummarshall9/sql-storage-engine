// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlMasks;
internal sealed partial class SqlMaskContextManagementService
{
    private static void ValidateCheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Type is null, nameof(SqlMaskContext.Type)), (string.IsNullOrWhiteSpace(value: sqlMaskContext?.Function), nameof(SqlMaskContext.Function))]);

    private static void ValidateApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Column is null, nameof(SqlMaskContext.Column)), (sqlMaskContext?.Column?.Type is null, nameof(sqlMaskContext.Column)), (sqlMaskContext?.Value is null, nameof(SqlMaskContext.Value))]);

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
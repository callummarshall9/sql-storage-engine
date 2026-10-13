// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskValues;
internal sealed partial class SqlMaskValueService
{
    private static void ValidatePrepareSqlMaskContextDateTime(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Value is null, nameof(SqlMaskContext.Value))]);

    private static void ValidateApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Type is null, nameof(SqlMaskContext.Type)), (sqlMaskContext?.Value is null, nameof(SqlMaskContext.Value)), (sqlMaskContext?.Kind is null, nameof(SqlMaskContext.Kind)), (sqlMaskContext?.Kind is "email" or "partial" && sqlMaskContext.Text is null, nameof(SqlMaskContext.Text))]);

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
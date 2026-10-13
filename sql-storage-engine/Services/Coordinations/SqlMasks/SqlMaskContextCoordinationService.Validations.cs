// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlMasks;
internal sealed partial class SqlMaskContextCoordinationService
{
    private static void ValidateReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (string.IsNullOrWhiteSpace(value: sqlMaskContext?.Function), nameof(SqlMaskContext.Function))]);

    private static void ValidateCheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        ValidateReadSqlMaskContext(sqlMaskContext: sqlMaskContext);

    private static void ValidateApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Type is null, nameof(SqlMaskContext.Type)), (sqlMaskContext?.Value is null, nameof(SqlMaskContext.Value)), (sqlMaskContext?.Kind is null, nameof(SqlMaskContext.Kind))]);

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
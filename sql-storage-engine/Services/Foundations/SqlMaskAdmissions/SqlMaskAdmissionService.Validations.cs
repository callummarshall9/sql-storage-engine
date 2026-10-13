// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskAdmissions;
internal sealed partial class SqlMaskAdmissionService
{
    private static void ValidateCheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Kind is null, nameof(SqlMaskContext.Kind)), (!Enum.IsDefined(value: sqlMaskContext!.Family), nameof(SqlMaskContext.Family))]);

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
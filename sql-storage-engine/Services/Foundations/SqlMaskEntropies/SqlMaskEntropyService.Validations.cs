// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskEntropies;
internal sealed partial class SqlMaskEntropyService
{
    private static void ValidateReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        Validate(inputs: [(sqlMaskContext is null, nameof(sqlMaskContext)), (sqlMaskContext?.Kind is not ("random" or "datetime"), nameof(SqlMaskContext.Kind)), (sqlMaskContext?.Kind == "random" && sqlMaskContext.Start > sqlMaskContext.End, nameof(SqlMaskContext.Start))]);

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
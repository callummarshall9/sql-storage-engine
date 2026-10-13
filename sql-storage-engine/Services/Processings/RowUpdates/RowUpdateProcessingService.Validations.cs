// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.RowUpdates;
internal sealed partial class RowUpdateProcessingService
{
    private static void ValidateRowUpdate(RowUpdate rowUpdate) =>
        Validate(inputs: [(rowUpdate is null, nameof(rowUpdate)), (rowUpdate?.ColumnSource is null && rowUpdate?.Columns is null, "columns")]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
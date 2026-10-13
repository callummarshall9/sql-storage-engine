// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowTextPayloadOrchestrationService
{
    private static void ValidateContext(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column.Type);
    }

    private static void ValidateValue(RowPayloadContext rowPayloadContext)
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Value);
    }

    private static void ValidateSource(RowPayloadContext rowPayloadContext)
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Source);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }

    private static void ValidateUtf8Text(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Value);
        Validate(inputs: [(rowPayloadContext.Value is not TextSqlValue, nameof(rowPayloadContext.Value))]);
    }

    private static void ValidateUtf8Source(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Source);
    }

    private static void ValidateColumnSetText(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Text);
    }

    private static void ValidateColumnSetElements(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.ColumnSetElements);
    }
}
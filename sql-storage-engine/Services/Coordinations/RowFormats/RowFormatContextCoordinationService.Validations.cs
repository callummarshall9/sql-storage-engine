// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.RowFormats;
internal sealed partial class RowFormatContextCoordinationService
{
    private static void ValidateContext(RowEncodingContext rowEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext);
    }

    private static void ValidateTable(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Table);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Table.Columns);
    }

    private static void ValidateRow(RowEncodingContext rowEncodingContext)
    {
        ValidateTable(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Row);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Row.Values);
        Validate(inputs: [(rowEncodingContext.Row.Values.Count != rowEncodingContext.Table!.Columns.Count, nameof(rowEncodingContext.Row))]);
    }

    private static void ValidateSource(RowEncodingContext rowEncodingContext)
    {
        ValidateTable(rowEncodingContext: rowEncodingContext);
        Validate(inputs: [(rowEncodingContext.Source.Length > RowFormat.MaximumEncodedLength, nameof(rowEncodingContext.Source))]);
    }

    private static void ValidateSelected(RowEncodingContext rowEncodingContext)
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.ColumnIds);
    }

    private static void ValidateColumn(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Column);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Column.Type);
    }

    private static void ValidateValue(RowEncodingContext rowEncodingContext)
    {
        ValidateColumn(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Value);
    }

    private static void ValidateColumnSource(RowEncodingContext rowEncodingContext)
    {
        ValidateColumn(rowEncodingContext: rowEncodingContext);
        Validate(inputs: [(rowEncodingContext.Source.Length > RowFormat.MaximumEncodedLength, nameof(rowEncodingContext.Source))]);
    }

    private static void ValidateSpatial(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Value);
        Validate(inputs: [(rowEncodingContext.Value is not SpatialSqlValue, nameof(rowEncodingContext.Value))]);
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

    private static void ValidateColumnSetText(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Text);
    }

    private static void ValidateColumnSetElements(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.ColumnSetElements);
    }
}
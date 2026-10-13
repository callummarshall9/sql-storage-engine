// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.RowCodecs;
internal sealed partial class RowCodecContextManagementService
{
    private void ValidateContext(RowEncodingContext rowEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext);
        Validate(inputs: [(configuration.InlineThreshold < 0 || configuration.InlineThreshold > RowFormat.MaximumInlineValueLength, nameof(configuration.InlineThreshold)), (configuration.UseOverflow && !overflowSourceBound, nameof(configuration.OverflowService))]);
    }

    private void ValidateTable(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Table);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Table.Columns);
    }

    private void ValidateRow(RowEncodingContext rowEncodingContext)
    {
        ValidateTable(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Row);
    }

    private void ValidateSource(RowEncodingContext rowEncodingContext)
    {
        ValidateTable(rowEncodingContext: rowEncodingContext);
        Validate(inputs: [(rowEncodingContext.Source.Length > RowFormat.MaximumEncodedLength, nameof(rowEncodingContext.Source))]);
    }

    private void ValidateSelected(RowEncodingContext rowEncodingContext)
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.ColumnIds);
    }

    private void ValidateValue(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Column);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Value);
    }

    private void ValidateUpdate(RowEncodingContext rowEncodingContext)
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Update);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Update.Columns);
    }

    private void ValidateStorage(RowEncodingContext rowEncodingContext)
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
    }

    private void ValidateRowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateRow(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(!configuration.UseOverflow, nameof(configuration.UseOverflow))]);
    }

    private void ValidateSourceAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(!configuration.UseOverflow, nameof(configuration.UseOverflow))]);
    }

    private void ValidateUpdateAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateUpdate(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(!configuration.UseOverflow, nameof(configuration.UseOverflow))]);
    }

    private void ValidateConversion(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.TargetType);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Value);
    }

    private void ValidateProjectionType(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        Validate(inputs: [(rowEncodingContext.ProjectionTypeName is not (SqlTypeName.NVarChar or SqlTypeName.Xml or SqlTypeName.Bit), nameof(rowEncodingContext.ProjectionTypeName))]);
    }

    private void ValidateReference(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Reference);
        Validate(inputs: [(!configuration.UseOverflow, nameof(configuration.UseOverflow))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.ParameterName);
            }
        }
    }

    private void ValidateColumnSetText(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Text);
    }

    private void ValidateColumnSetElements(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.ColumnSetElements);
    }
}
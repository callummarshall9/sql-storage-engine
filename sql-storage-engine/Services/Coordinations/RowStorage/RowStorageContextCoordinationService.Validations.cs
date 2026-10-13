// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Coordinations.RowStorage;
internal sealed partial class RowStorageContextCoordinationService
{
    private static void ValidateContext(RowEncodingContext rowEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext);
    }

    private static void ValidateMetadata(RowEncodingContext rowEncodingContext)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Table);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Row);
    }

    private void ValidateWrite(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(rowEncodingContext.Source.IsEmpty || rowEncodingContext.Source.Length > OverflowReferenceFormat.MaximumValueLength, nameof(rowEncodingContext.Source)), (overflow is null, "Overflow service is unavailable.")]);
    }

    private void ValidateReference(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(rowEncodingContext.Reference is null, nameof(rowEncodingContext.Reference)), (overflow is null, "Overflow service is unavailable.")]);
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
}
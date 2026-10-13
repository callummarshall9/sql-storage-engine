// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Coordinations.TableHeaps;
internal sealed partial class TableHeapContextCoordinationService
{
    private static void ValidateContextSource(TableHeapContext tableHeapContext)
    {
        ArgumentNullException.ThrowIfNull(argument: tableHeapContext);
    }

    private static void ValidateContext(TableHeapContext tableHeapContext, CancellationToken cancellationToken)
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateMaximumPages(TableHeapContext tableHeapContext, CancellationToken cancellationToken)
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: tableHeapContext.MaximumPages);
    }

    private static void ValidateSample(TableHeapContext tableHeapContext, CancellationToken cancellationToken)
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        if (tableHeapContext.PagePercentage is < 0 or > 100 || tableHeapContext.RepeatableSeed is < 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(tableHeapContext));
        }
    }

    private static void ValidateRequiredBytes(TableHeapContext tableHeapContext)
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);
        ArgumentOutOfRangeException.ThrowIfNegative(value: tableHeapContext.RequiredBytes);
    }

    private static void ValidateFreeSpace(TableHeapContext tableHeapContext, int freeBytes)
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);
        ArgumentOutOfRangeException.ThrowIfNegative(value: freeBytes);
    }

    private static void ValidateTableHeapContextOnDelete(TableHeapContext tableHeapContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(tableHeapContext is null, nameof(tableHeapContext))]);
        ValidateContext(tableHeapContext: tableHeapContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateTableHeapContextOnUpdate(TableHeapContext tableHeapContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(tableHeapContext is null, nameof(tableHeapContext))]);
        ValidateContext(tableHeapContext: tableHeapContext!, cancellationToken: cancellationToken);
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

    private static void ValidateTableHeapContextOnAdd(TableHeapContext newTableHeapContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newTableHeapContext is null, nameof(newTableHeapContext)), (newTableHeapContext is not null && newTableHeapContext.Row.IsEmpty, "row")]);
        ValidateContext(tableHeapContext: newTableHeapContext!, cancellationToken: cancellationToken);
    }
}
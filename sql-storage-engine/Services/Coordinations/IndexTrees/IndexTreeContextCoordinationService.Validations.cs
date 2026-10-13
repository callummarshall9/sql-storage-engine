// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Coordinations.IndexTrees;
internal sealed partial class IndexTreeContextCoordinationService
{
    private static void ValidateContext(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateKey(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateContext(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext.Key);
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext.Key.Content);
        Validate(inputs: [(indexTreeContext.Key.Content.Any(predicate: value => value > byte.MaxValue), nameof(indexTreeContext.Key))]);
    }

    private static void ValidateRange(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateContext(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext.Range);
        Validate(inputs: [(!Enum.IsDefined(value: indexTreeContext.Range.Value.Direction), nameof(indexTreeContext.Range))]);
    }

    private static void ValidateIndexTreeContextOnAdd(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        Validate(inputs: [(indexTreeContext.RowId.PageId.Value == 0, nameof(indexTreeContext.RowId)), (indexTreeContext.IncludedPayload.Length > ushort.MaxValue, nameof(indexTreeContext.IncludedPayload))]);
    }

    private static void ValidateIndexTreeContextOnDelete(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        Validate(inputs: [(indexTreeContext.RowId.PageId.Value == 0, nameof(indexTreeContext.RowId))]);
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

    private static void ValidateBinding(IndexTreeContext indexTreeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext);
        ArgumentNullException.ThrowIfNull(argument: indexTreeContext.Definition);
        Validate(inputs: [(indexTreeContext.AddCount < 0, nameof(indexTreeContext.AddCount)), (indexTreeContext.RemoveCount < 0, nameof(indexTreeContext.RemoveCount))]);
    }

    private static void ValidateIndexTreeContextWithoutSplitOnAdd(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        Validate(inputs: [(indexTreeContext.RowId.PageId.Value == 0, nameof(indexTreeContext.RowId)), (indexTreeContext.IncludedPayload.Length > ushort.MaxValue, nameof(indexTreeContext.IncludedPayload))]);
    }

    private static void ValidateIndexTreeContextBindingOnAdd(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        ValidateBinding(indexTreeContext: indexTreeContext);
        Validate(inputs: [(indexTreeContext.RowId.PageId.Value == 0, nameof(indexTreeContext.RowId)), (indexTreeContext.IncludedPayload.Length > ushort.MaxValue, nameof(indexTreeContext.IncludedPayload)), (indexTreeContext.Binding is null, nameof(indexTreeContext.Binding))]);
    }

    private static void ValidateIndexTreeContextBindingOnDelete(IndexTreeContext indexTreeContext, CancellationToken cancellationToken)
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        ValidateBinding(indexTreeContext: indexTreeContext);
        Validate(inputs: [(indexTreeContext.RowId.PageId.Value == 0, nameof(indexTreeContext.RowId)), (indexTreeContext.Binding is null, nameof(indexTreeContext.Binding))]);
    }
}
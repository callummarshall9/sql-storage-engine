// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Managements.HeapIndexMutations;
internal sealed partial class HeapIndexMutationManagementService
{
    private static void ValidateIndexRegistration(HeapIndexMutationContext heapIndexMutationContext)
    {
        Validate(inputs: [(heapIndexMutationContext is null, nameof(heapIndexMutationContext)), (heapIndexMutationContext?.IndexBindings is null, nameof(HeapIndexMutationContext.IndexBindings))]);

        foreach (var binding in heapIndexMutationContext!.IndexBindings!)
        {
            Validate(inputs: [(binding is null, nameof(HeapIndexMutationContext.IndexBindings)), (binding?.Definition is null, nameof(HeapIndexMutationContext.IndexBindings)), (binding?.Service is null, nameof(HeapIndexMutationContext.IndexBindings))]);
        }
    }

    private void ValidateLockContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(heapIndexMutationContext is null, nameof(heapIndexMutationContext)), (locking is not null && lockState.Transaction is null, nameof(HeapIndexMutationContext.Transaction)), (locking is not null && !Enum.IsDefined(value: lockState.IsolationLevel), nameof(HeapIndexMutationContext.IsolationLevel)), (lockState.HeldRows is null, nameof(HeapIndexMutationContext.HeldRows))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: heapIndexMutationContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateIndex(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: heapIndexMutationContext.Index);
        ArgumentNullException.ThrowIfNull(argument: heapIndexMutationContext.Index.Context);
        ArgumentNullException.ThrowIfNull(argument: heapIndexMutationContext.Index.Definition);
        ArgumentNullException.ThrowIfNull(argument: heapIndexMutationContext.Key);
    }

    private static void ValidateHeapIndexMutationContextHeapOnAdd(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateContext(heapIndexMutationContext: newHeapIndexMutationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(newHeapIndexMutationContext.Bytes.IsEmpty, nameof(newHeapIndexMutationContext.Bytes))]);
    }

    private static void ValidateHeapIndexMutationContextHeapOnUpdate(HeapIndexMutationContext updatedHeapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateContext(heapIndexMutationContext: updatedHeapIndexMutationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(updatedHeapIndexMutationContext.Bytes.IsEmpty, nameof(updatedHeapIndexMutationContext.Bytes))]);
    }

    private static void ValidateHeapIndexMutationContextHeapOnDelete(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateContext(heapIndexMutationContext: deletedHeapIndexMutationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(deletedHeapIndexMutationContext is null, nameof(deletedHeapIndexMutationContext))]);
    }

    private static void ValidateHeapIndexMutationContextIndexOnAdd(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateIndex(heapIndexMutationContext: newHeapIndexMutationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(newHeapIndexMutationContext is null, nameof(newHeapIndexMutationContext))]);
    }

    private static void ValidateHeapIndexMutationContextIndexOnDelete(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken)
    {
        ValidateIndex(heapIndexMutationContext: deletedHeapIndexMutationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(deletedHeapIndexMutationContext is null, nameof(deletedHeapIndexMutationContext))]);
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
}
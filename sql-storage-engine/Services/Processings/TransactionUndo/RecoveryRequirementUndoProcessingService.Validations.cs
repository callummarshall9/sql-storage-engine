// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.TransactionUndo;
internal sealed partial class RecoveryRequirementUndoProcessingService
{
    private void ValidateUndo(TransactionMutationKind kind, Action undo)
    {
        Validate(inputs: [(!Enum.IsDefined(value: kind), false, nameof(kind)), (undo is null, true, nameof(undo))]);
        ValidateOpen();
    }

    private void ValidateBeforeImage(TransactionMutationKind kind, Memory<byte> destination, ReadOnlySpan<byte> beforeImage)
    {
        Validate(inputs: [(!Enum.IsDefined(value: kind), false, nameof(kind)), (destination.Length != beforeImage.Length, false, nameof(beforeImage))]);
        ValidateOpen();
    }

    private void ValidateAllocatedPageId(PageId pageId, Action reclaim)
    {
        Validate(inputs: [(pageId.Value == 0, false, nameof(pageId)), (reclaim is null, true, nameof(reclaim))]);
        ValidateOpen();
    }

    private void ValidateRetiredPageId(PageId pageId)
    {
        Validate(inputs: [(pageId.Value == 0, false, nameof(pageId))]);
        ValidateOpen();
    }

    private void ValidateOpen() =>
        Validate(inputs: [(state.Completed, "Transaction undo log is already complete or requires recovery.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "Undo input is invalid.", paramName: input.ParameterName);
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new InvalidOperationException(message: input.Message);
            }
        }
    }
}
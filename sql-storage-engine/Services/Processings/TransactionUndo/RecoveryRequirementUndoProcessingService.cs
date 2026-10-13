// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Foundations.RecoveryRequirements;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.TransactionUndo;
/// <summary>Owns undo ordering and page ownership evidence for one in-process transaction.</summary>
internal sealed partial class RecoveryRequirementUndoProcessingService(IRecoveryRequirementService recoveryRequirements) : IRecoveryRequirementUndoProcessingService
{
    private readonly RecoveryRequirement recoveryRequirement = new();
    private readonly TransactionUndoState state = new()
    {
        Undo = [],
        Allocated = [],
        Retired = []
    };
    public IReadOnlyList<PageId> GetAllocatedPageIds() =>
        TryCatch(operation: () =>
    {
        var pageIds = state.Allocated.Select(selector: item => item.PageId)
            .ToArray();

        return Array.AsReadOnly(array: pageIds);
    });

    public IReadOnlyList<PageId> GetRetiredPageIds() =>
        TryCatch(operation: () => Array.AsReadOnly(array: state.Retired.ToArray()));

    public bool IsRecoveryRequired() =>
        TryCatch(operation: () => recoveryRequirements.ReadRecoveryRequirement(recoveryRequirement: recoveryRequirement));

    public void RecordUndo(TransactionMutationKind kind, Action undo) =>
        TryCatch(operation: () =>
    {
        ValidateUndo(kind: kind, undo: undo);
        state.Undo.Add(item: (kind, undo));
    });

    public void RecordBeforeImage(TransactionMutationKind kind, Memory<byte> destination, ReadOnlySpan<byte> beforeImage) =>
        TryCatch(operation: context =>
    {
        ValidateBeforeImage(kind: context.kind, destination: context.destination, beforeImage: context.beforeImage);
        var snapshot = context.beforeImage.ToArray();
        var destination = context.destination;
        state.Undo.Add(item: (context.kind, () => snapshot.CopyTo(destination: destination)));
    }, context: new TransactionBeforeImageContext { kind = kind, destination = destination, beforeImage = beforeImage });

    public void TrackAllocatedPageId(PageId pageId, Action reclaim) =>
        TryCatch(operation: () =>
    {
        ValidateAllocatedPageId(pageId: pageId, reclaim: reclaim);
        state.Allocated.Add(item: (pageId, reclaim));
    });

    public void TrackRetiredPageId(PageId pageId) =>
        TryCatch(operation: () =>
    {
        ValidateRetiredPageId(pageId: pageId);
        state.Retired.Add(item: pageId);
    });

    public void Commit() =>
        TryCatch(operation: () =>
    {
        ValidateOpen();
        state.Completed = true;
        state.Undo.Clear();
        state.Allocated.Clear();
    });

    public void Rollback() =>
        TryCatch(operation: () =>
    {
        ValidateOpen();
        RollbackCore();
    });

    private void RollbackCore()
    {
        try
        {
            for (var index = state.Undo.Count - 1; index >= 0; index--)
            {
                state.Undo[index].Undo();
            }

            for (var index = state.Allocated.Count - 1; index >= 0; index--)
            {
                state.Allocated[index].Reclaim();
            }

            state.Completed = true;
            state.Retired.Clear();
        }
        catch
        {
            state.Completed = true;
            recoveryRequirements.MarkRecoveryRequirement(recoveryRequirement: recoveryRequirement);
            throw;
        }
    }
}
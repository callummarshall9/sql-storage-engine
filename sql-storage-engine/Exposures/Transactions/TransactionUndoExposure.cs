// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.RecoveryRequirements;
using sql_storage_engine.Services.Processings.TransactionUndo;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Exposures.Transactions;
/// <summary>Records transaction undo actions and page ownership changes in mutation order.</summary>
public sealed class TransactionUndoExposure
{
    private readonly IRecoveryRequirementUndoProcessingService service = new RecoveryRequirementUndoProcessingService(recoveryRequirements: new RecoveryRequirementService());
    public IReadOnlyList<PageId> GetAllocatedPageIds() =>
        service.GetAllocatedPageIds();

    public IReadOnlyList<PageId> GetRetiredPageIds() =>
        service.GetRetiredPageIds();

    public bool IsRecoveryRequired() =>
        service.IsRecoveryRequired();

    public void RecordUndo(TransactionMutationKind kind, Action undo) =>
        service.RecordUndo(kind: kind, undo: undo);

    public void RecordBeforeImage(TransactionMutationKind kind, Memory<byte> destination, ReadOnlySpan<byte> beforeImage) =>
        service.RecordBeforeImage(kind: kind, destination: destination, beforeImage: beforeImage);

    public void TrackAllocatedPageId(PageId pageId, Action reclaim) =>
        service.TrackAllocatedPageId(pageId: pageId, reclaim: reclaim);

    public void TrackRetiredPageId(PageId pageId) =>
        service.TrackRetiredPageId(pageId: pageId);

    public void Commit() =>
        service.Commit();

    public void Rollback() =>
        service.Rollback();
}
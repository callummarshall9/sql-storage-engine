// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.TransactionUndo;
internal interface IRecoveryRequirementUndoProcessingService
{
    IReadOnlyList<PageId> GetAllocatedPageIds();

    IReadOnlyList<PageId> GetRetiredPageIds();

    bool IsRecoveryRequired();

    void RecordUndo(TransactionMutationKind kind, Action undo);

    void RecordBeforeImage(TransactionMutationKind kind, Memory<byte> destination, ReadOnlySpan<byte> beforeImage);

    void TrackAllocatedPageId(PageId pageId, Action reclaim);

    void TrackRetiredPageId(PageId pageId);

    void Commit();

    void Rollback();
}
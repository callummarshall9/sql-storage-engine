// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Models.Transactions;
internal sealed class TransactionUndoState
{
    public List<(TransactionMutationKind Kind, Action Undo)> Undo { get => undo!; init => undo = value; }
    public List<(PageId PageId, Action Reclaim)> Allocated { get => allocated!; init => allocated = value; }
    public List<PageId> Retired { get => retired!; init => retired = value; }

    private List<(TransactionMutationKind Kind, Action Undo)>? undo;
    private List<(PageId PageId, Action Reclaim)>? allocated;
    private List<PageId>? retired;
    public bool Completed { get; set; }
}
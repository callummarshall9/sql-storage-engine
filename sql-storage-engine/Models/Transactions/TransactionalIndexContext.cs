// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Transactions;
internal sealed record TransactionalIndexContext
{
    public IndexId IndexId { get; set; }
    public TransactionIsolationLevel IsolationLevel { get; set; }
    public LockingTransactionContext? Transaction { get; set; }
    public IndexKey? Key { get; set; }
    public RowId RowId { get; set; }
    public IndexRange Range { get; set; }
    public IReadOnlyList<LeafIndexEntry>? Entries { get; set; }
    public IndexDeleteResult? DeleteResult { get; set; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Models.Tables;
internal sealed class HeapIndexMutationConfiguration
{
    public LockingTransactionBinding? Transaction { get; set; }
    public TableId TableId { get; set; }
    public TransactionIsolationLevel IsolationLevel { get; set; }
}
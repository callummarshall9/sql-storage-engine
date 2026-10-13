// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Models.Tables;
internal sealed record HeapIndexMutationContext
{
    public TableIndexBinding[]? IndexBindings { get; init; }
    public TableIndexBinding? Index { get; init; }
    public IndexKey? Key { get; init; }
    public RowId RowId { get; init; }
    public RowId ResultRowId { get; set; }
    public ReadOnlyMemory<byte> Bytes { get; init; }
    public ReadOnlyMemory<byte> IncludedPayload { get; init; }
    public TableHeapLookupResult LookupResult { get; set; }
    public HeapUpdateResult UpdateResult { get; set; }
    public ReadOnlyMemory<byte> ResultBytes { get; set; }
    public bool Result { get; set; }
    public IReadOnlyList<RowId>? IndexRows { get; set; }
    public IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)>? Rows { get; set; }
    public decimal PagePercentage { get; init; }
    public long? RepeatableSeed { get; init; }
    public long LiveRowCount { get; set; }
    public LockingTransactionContext? Transaction { get; init; }
    public Dictionary<RowLockResource, LockMode>? HeldRows { get; init; }
    public TableId TableId { get; init; }
    public TransactionIsolationLevel IsolationLevel { get; init; }
    public bool ReadLockAcquired { get; set; }
}
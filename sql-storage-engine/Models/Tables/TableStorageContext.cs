// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Tables;
internal sealed record TableStorageContext
{
    public ulong MutationId { get; init; }
    public sql_storage_engine.Models.Transactions.LockingTransactionBinding? LockingTransaction { get; init; }
    public sql_storage_engine.Models.Transactions.TransactionIsolationLevel IsolationLevel { get; init; }
    public TableStorageContext? Binding { get; init; }
    public CatalogTable? Definition { get; set; }
    public TableDefinition? Schema { get; set; }
    public TableHeapBinding? HeapBinding { get; init; }
    public TableIndexBinding[]? Indexes { get; set; }
    public Row? Row { get; set; }
    public RowUpdate? Update { get; set; }
    public RowId RowId { get; init; }
    public SqlValue? PeriodStart { get; init; }
    public bool GraphMutation { get; init; }
    public bool Prepared { get; init; }
    public bool Found { get; set; }
    public TableInsertResult? InsertResult { get; set; }
    public TableUpdateResult? UpdateResult { get; set; }
    public TableDeleteResult? DeleteResult { get; set; }
    public IAsyncEnumerable<StoredRow>? Rows { get; set; }
    public decimal PagePercentage { get; init; }
    public long? RequestedRows { get; init; }
    public long? RepeatableSeed { get; init; }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Transactions;
public enum TransactionMutationKind
{
    HeapInsert,
    HeapUpdate,
    HeapDelete,
    IndexSplit,
    OverflowReplacement,
    CatalogChange
}
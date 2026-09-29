using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

public enum TransactionMutationKind { HeapInsert, HeapUpdate, HeapDelete, IndexSplit, OverflowReplacement, CatalogChange }

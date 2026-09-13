namespace sql_storage_engine;

public enum StorageTransactionState { Committable, Doomed, Committed, Aborted, Indeterminate }

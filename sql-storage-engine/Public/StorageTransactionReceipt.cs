namespace sql_storage_engine;

public sealed record StorageTransactionReceipt(StorageTransactionIdentity Identity, StorageTransactionState State);

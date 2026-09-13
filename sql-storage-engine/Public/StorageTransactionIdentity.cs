using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

public readonly record struct StorageTransactionIdentity(DatabaseId DatabaseId, Guid Value);

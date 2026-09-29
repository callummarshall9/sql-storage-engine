using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Identifies a logical resource independently of its in-memory or on-disk location.</summary>
public abstract record LockResource;

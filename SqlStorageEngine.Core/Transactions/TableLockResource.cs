using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Identifies all rows and indexes belonging to a table.</summary>
public sealed record TableLockResource(TableId TableId) : LockResource;

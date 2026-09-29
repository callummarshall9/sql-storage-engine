using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Identifies one generation-safe row within a table.</summary>
public sealed record RowLockResource(TableId TableId, RowId RowId) : LockResource;

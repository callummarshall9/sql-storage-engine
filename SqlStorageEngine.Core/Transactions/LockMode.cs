using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Describes the access granted to a transaction for one logical resource.</summary>
public enum LockMode
{
    Shared = 1,
    Update = 2,
    Exclusive = 3
}

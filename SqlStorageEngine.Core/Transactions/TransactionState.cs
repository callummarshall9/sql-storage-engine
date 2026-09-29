using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

/// <summary>Describes the externally observable lifecycle of a transaction.</summary>
public enum TransactionState
{
    Active = 1,
    Committed = 2,
    RolledBack = 3,
    Failed = 4
}

// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
/// <summary>Defines the row-lock retention policy used by a transactional table session.</summary>
public enum TransactionIsolationLevel
{
    ReadCommitted = 1,
    RepeatableRead = 2,
    Serializable = 3
}
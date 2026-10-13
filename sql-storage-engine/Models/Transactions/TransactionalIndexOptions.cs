// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
public sealed record TransactionalIndexOptions
{
    public TransactionIsolationLevel IsolationLevel { get; init; }
}
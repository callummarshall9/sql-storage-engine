// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
public sealed record TransactionalTableOptions
{
    public TransactionIsolationLevel IsolationLevel { get; init; }
}
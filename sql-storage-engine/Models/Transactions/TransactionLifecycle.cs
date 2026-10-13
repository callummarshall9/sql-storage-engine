// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Transactions;
public sealed record TransactionLifecycle
{
    public TransactionId TransactionId { get; init; }
    public Action? Commit { get; init; }
    public Action? Rollback { get; init; }
    internal TransactionLifecycleState? LifecycleState { get; init; }
}
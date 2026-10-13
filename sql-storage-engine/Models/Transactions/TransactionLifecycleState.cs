// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Models.Transactions;
internal sealed class TransactionLifecycleState
{
    public object? Sync;
    public ulong Id;
    public TransactionState Phase;
    public Action? Commit;
    public Action? Rollback;
}
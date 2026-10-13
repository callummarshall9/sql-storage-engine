// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Models.Transactions;
internal sealed record LockingTransactionContext
{
    public TransactionLifecycle? Lifecycle { get; set; }
    public LockResource? Resource { get; set; }
    public LockMode Mode { get; set; }
    public Action? ReleasePins { get; set; }
    public bool Released { get; set; }
    public TransactionId TransactionId { get; set; }
    public TransactionState Phase { get; set; }
}
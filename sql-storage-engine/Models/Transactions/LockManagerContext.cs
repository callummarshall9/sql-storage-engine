// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Models.Transactions;
internal sealed record LockManagerContext
{
    public TransactionId TransactionId { get; init; }
    public LockResource? LockResource { get; init; }
    public LockMode Mode { get; init; }
    public Action? VictimHandler { get; init; }
    public TransactionId? LastVictim { get; init; }
}
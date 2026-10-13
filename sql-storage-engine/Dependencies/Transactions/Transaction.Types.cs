// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;

namespace sql_storage_engine.Transactions;
/// <summary>Describes the externally observable lifecycle of a transaction.</summary>
public enum TransactionState
{
    Active = 1,
    Committed = 2,
    RolledBack = 3,
    Failed = 4
}

/// <summary>Represents one atomic unit of storage work.</summary>
public interface ITransaction : IDisposable
{
    TransactionId ReadId();

    TransactionState ReadState();

    void EnsureActive();

    void Commit();

    void Rollback();
}
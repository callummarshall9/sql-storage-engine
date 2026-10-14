// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Transactions;
/// <summary>Reports that a deterministic deadlock victim was rolled back so surviving transactions can continue.</summary>
public sealed class DeadlockException : StorageException
{
    public DeadlockException(TransactionId victimTransactionId) : base($"Transaction {victimTransactionId} was selected as the deadlock victim.") => VictimTransactionId = victimTransactionId;
    public DeadlockException(TransactionId victimTransactionId, Exception innerException) : base(message: $"Transaction {victimTransactionId} was selected as the deadlock victim.", innerException: innerException) => VictimTransactionId = victimTransactionId;
    public TransactionId VictimTransactionId { get; }
}
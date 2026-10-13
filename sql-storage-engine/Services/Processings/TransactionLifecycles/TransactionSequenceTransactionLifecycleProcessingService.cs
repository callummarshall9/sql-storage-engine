// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.TransactionSequences;

namespace sql_storage_engine.Services.Processings.TransactionLifecycles;
internal sealed partial class TransactionSequenceTransactionLifecycleProcessingService : ITransactionSequenceTransactionLifecycleProcessingService
{
    private readonly TransactionSequenceService sequence;
    public TransactionSequenceTransactionLifecycleProcessingService(TransactionSequenceConfiguration configuration)
    {
        sequence = new TransactionSequenceService(configuration: configuration);
    }

    public TransactionLifecycle CreateTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionLifecycleCreation(transactionLifecycle: transactionLifecycle);
        var id = sequence.NextTransactionSequence();

        return new TransactionLifecycle
        {
            TransactionId = new TransactionId
            {
                Value = id
            },
            LifecycleState = CreateTransactionLifecycleState(transactionId: id, commit: transactionLifecycle.Commit, rollback: transactionLifecycle.Rollback)
        };
    });

    private static TransactionLifecycleState CreateTransactionLifecycleState(ulong transactionId, Action? commit, Action? rollback) =>
        new TransactionLifecycleState
    {
        Sync = new object (),
        Id = transactionId,
        Phase = TransactionState.Active,
        Commit = commit,
        Rollback = rollback
    };
}
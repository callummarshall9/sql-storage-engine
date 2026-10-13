// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.TransactionSequences;
internal sealed partial class TransactionSequenceService : ITransactionSequenceService
{
    private readonly TransactionSequenceState state;
    public TransactionSequenceService(TransactionSequenceConfiguration configuration)
    {
        state = CreateTransactionSequenceState(configuration: configuration);
    }

    private static TransactionSequenceState CreateTransactionSequenceState(TransactionSequenceConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionSequenceConfiguration(configuration: configuration);

        return new TransactionSequenceState
        {
            LastId = checked((long)configuration.LastAllocatedId)
        };
    });

    public ulong NextTransactionSequence() =>
        TryCatch(operation: () =>
    {
        while (true)
        {
            var current = Interlocked.Read(location: ref state.LastId);

            if (current == long.MaxValue)
            {
                throw new InvalidOperationException(message: "Transaction ID space is exhausted.");
            }

            var next = current + 1;

            if (Interlocked.CompareExchange(location1: ref state.LastId, value: next, comparand: current) == current)
            {
                return (ulong)next;
            }
        }
    });
}
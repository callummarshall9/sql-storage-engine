// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Processings.TransactionLifecycles;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class TransactionManagerExposure
{
    private readonly TransactionSequenceTransactionLifecycleProcessingService service;
    public TransactionManagerExposure(ulong lastAllocatedId = 0)
    {
        service = new TransactionSequenceTransactionLifecycleProcessingService(configuration: new TransactionSequenceConfiguration { LastAllocatedId = lastAllocatedId });
    }

    public TransactionLifecycle CreateTransactionLifecycle(Action? commit = null, Action? rollback = null) =>
        service.CreateTransactionLifecycle(transactionLifecycle: new TransactionLifecycle { Commit = commit, Rollback = rollback });
}
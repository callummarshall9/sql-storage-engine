// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.TransactionLifecycles;
using sql_storage_engine.Services.Foundations.TransactionIdentities;
using sql_storage_engine.Services.Orchestrations.TransactionLifecycles;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class TransactionExposure : ITransaction, ITransactionLifecycleExposure, cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    internal static readonly ITransactionLifecycleOrchestrationService service = new TransactionLifecycleOrchestrationService(identities: new TransactionIdentityService(), transitions: new TransactionLifecycleService());
    internal readonly TransactionLifecycle transactionLifecycle;
    public TransactionExposure(TransactionLifecycle transactionLifecycle)
    {
        this.transactionLifecycle = service.PrepareTransactionLifecycle(transactionLifecycle: transactionLifecycle);
    }

    public TransactionId ReadId() =>
        new TransactionId
    {
        Value = service.ReadTransactionLifecycleId(transactionLifecycle: transactionLifecycle)
    };

    public TransactionState ReadState() =>
        service.ReadTransactionLifecycleState(transactionLifecycle: transactionLifecycle);

    public void EnsureActive() =>
        service.EnsureActiveTransactionLifecycle(transactionLifecycle: transactionLifecycle);

    public void Commit() =>
        service.CommitTransactionLifecycle(transactionLifecycle: transactionLifecycle);

    public void Rollback() =>
        service.RollbackTransactionLifecycle(transactionLifecycle: transactionLifecycle);

    public void Dispose() =>
        service.CloseTransactionLifecycle(transactionLifecycle: transactionLifecycle);

    public TransactionLifecycle ReadTransactionLifecycle() =>
        service.PrepareTransactionLifecycle(transactionLifecycle: transactionLifecycle);
}
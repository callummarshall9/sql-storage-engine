// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionLifecycles;
internal sealed partial class TransactionLifecycleService : ITransactionLifecycleService
{
    public TransactionState ReadTransactionLifecycleState(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);

        lock (state.Sync!)
        {
            return state.Phase;
        }
    });

    public void EnsureActiveTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);

        lock (state.Sync!)
        {
            ValidateActiveState(state: state, operation: "storage operations");
        }
    });

    public void CommitTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);

        lock (state.Sync!)
        {
            ValidateActiveState(state: state, operation: "Commit");

            try
            {
                state.Commit?.Invoke();
                state.Phase = TransactionState.Committed;
            }
            catch
            {
                state.Phase = TransactionState.Failed;
                throw;
            }
        }
    });

    public void RollbackTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);

        lock (state.Sync!)
        {
            ValidateActiveState(state: state, operation: "Rollback");

            try
            {
                state.Rollback?.Invoke();
                state.Phase = TransactionState.RolledBack;
            }
            catch
            {
                state.Phase = TransactionState.Failed;
                throw;
            }
        }
    });

    public void CloseTransactionLifecycle(TransactionLifecycle transactionLifecycle) =>
        TryCatch(operation: () =>
    {
        var state = ValidateTransactionLifecycle(transactionLifecycle: transactionLifecycle);

        lock (state.Sync!)
        {
            if (state.Phase != TransactionState.Active)
            {
                return;
            }

            try
            {
                state.Rollback?.Invoke();
                state.Phase = TransactionState.RolledBack;
            }
            catch
            {
                state.Phase = TransactionState.Failed;
                throw;
            }
        }
    });
}
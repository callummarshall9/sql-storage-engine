// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Exposures.Transactions;
public interface ITransactionLifecycleExposure
{
    TransactionLifecycle ReadTransactionLifecycle();
}
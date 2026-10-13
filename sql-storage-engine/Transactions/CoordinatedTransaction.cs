// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Transactions;
/// <summary>Binds a transaction lifecycle to a database coordination lease.</summary>
public sealed class CoordinatedTransaction : IDisposable
{
    private readonly IDisposable _lease;
    public CoordinatedTransaction(sql_storage_engine.Exposures.Transactions.TransactionExposure transaction, IDisposable lease)
    {
        Transaction = transaction;
        _lease = lease;
    }

    public sql_storage_engine.Exposures.Transactions.TransactionExposure Transaction { get; }

    public void Commit()
    {
        try
        {
            Transaction.Commit();
        }
        finally
        {
            _lease.Dispose();
        }
    }

    public void Rollback()
    {
        try
        {
            Transaction.Rollback();
        }
        finally
        {
            _lease.Dispose();
        }
    }

    public void Dispose()
    {
        try
        {
            Transaction.Dispose();
        }
        finally
        {
            _lease.Dispose();
        }
    }
}
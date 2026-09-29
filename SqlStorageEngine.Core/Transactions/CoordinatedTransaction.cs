namespace sql_storage_engine.Transactions;

/// <summary>Binds a transaction lifecycle to a database coordination lease.</summary>
public sealed class CoordinatedTransaction : IDisposable
{
    private readonly IDisposable _lease;
    public CoordinatedTransaction(Transaction transaction, IDisposable lease)
    { Transaction = transaction; _lease = lease; }
    public Transaction Transaction { get; }
    public void Commit() { try { Transaction.Commit(); } finally { _lease.Dispose(); } }
    public void Rollback() { try { Transaction.Rollback(); } finally { _lease.Dispose(); } }
    public void Dispose() { try { Transaction.Dispose(); } finally { _lease.Dispose(); } }
}

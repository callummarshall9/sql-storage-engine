using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

/// <summary>Represents one atomic unit of storage work.</summary>
public interface ITransaction : IDisposable
{
    TransactionId Id { get; }
    TransactionState State { get; }
    void EnsureActive();
    void Commit();
    void Rollback();
}

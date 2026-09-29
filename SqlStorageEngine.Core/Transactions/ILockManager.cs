using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>
/// Coordinates transaction-owned logical locks. Acquisition and conversion wait until grantable and honor
/// cancellation without retaining a request. A transaction owns each granted lock until explicit release or
/// <see cref="ReleaseAll"/>; implementations must not transfer ownership between transaction IDs.
/// </summary>
public interface ILockManager
{
    ValueTask AcquireAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default);
    ValueTask ConvertAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default);
    bool Release(TransactionId transactionId, LockResource resource);
    void ReleaseAll(TransactionId transactionId);
}

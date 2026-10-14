// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.LockResources;

namespace sql_storage_engine.Transactions;
/// <summary>Describes the access granted to a transaction for one logical resource.</summary>
public enum LockMode
{
    Shared = 1,
    Update = 2,
    Exclusive = 3
}

/// <summary>
/// Coordinates transaction-owned logical locks. Acquisition and conversion wait until grantable and honor
/// cancellation without retaining a request. A transaction owns each granted lock until explicit release or
/// <see cref = "ReleaseAll"/>; implementations must not transfer ownership between transaction IDs.
/// </summary>
public interface ILockManager
{
    ValueTask AcquireAsync(TransactionId transactionId, LockResource resource, LockMode mode, CancellationToken cancellationToken = default);

    ValueTask ConvertAsync(TransactionId transactionId, LockResource resource, LockMode mode, CancellationToken cancellationToken = default);

    bool Release(TransactionId transactionId, LockResource resource);

    void ReleaseAll(TransactionId transactionId);
}
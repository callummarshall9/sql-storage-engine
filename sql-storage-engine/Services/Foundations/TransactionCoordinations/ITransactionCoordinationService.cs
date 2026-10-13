// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionCoordinations;
internal interface ITransactionCoordinationService
{
    ValueTask<ulong> AcquireTransactionCoordinationAsync(TransactionAccess access, CancellationToken cancellationToken, ulong? leaseId);

    void CloseTransactionCoordination(ulong? leaseId);
}
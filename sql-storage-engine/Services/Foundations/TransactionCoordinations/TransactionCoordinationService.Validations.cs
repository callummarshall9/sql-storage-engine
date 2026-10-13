// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Foundations.TransactionCoordinations;
internal sealed partial class TransactionCoordinationService
{
    private static void ValidateTransactionCoordinationAccess(TransactionAccess access, CancellationToken cancellationToken, ulong? leaseId)
    {
        if (!Enum.IsDefined(value: access))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(access));
        }

        ValidateTransactionCoordinationLease(leaseId: leaseId);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionCoordinationLease(ulong? leaseId)
    {
        if (leaseId == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(leaseId));
        }
    }

    private void ValidateOpenUnderLock()
    {
        if (state.Closed)
        {
            throw new ObjectDisposedException(objectName: "TransactionCoordinator");
        }
    }

    private void ValidateClosableUnderLock()
    {
        if (state.Pending != 0 || state.LeaseKinds!.Count != 0)
        {
            throw new InvalidOperationException(message: "Cannot close transaction coordination while acquisitions or leases remain active.");
        }
    }
}
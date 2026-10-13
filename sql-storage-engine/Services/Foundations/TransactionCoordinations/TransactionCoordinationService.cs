// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Brokers.Coordination;

namespace sql_storage_engine.Services.Foundations.TransactionCoordinations;
internal sealed partial class TransactionCoordinationService : ITransactionCoordinationService
{
    private const byte Turnstile = 0;
    private const byte Resource = 1;
    private const byte ReaderMutex = 2;
    private readonly TransactionCoordinationBroker broker = new();
    private readonly TransactionCoordinationState state = new()
    {
        Sync = new object (),
        LeaseKinds = new Dictionary<ulong, bool>()
    };
    public ValueTask<ulong> AcquireTransactionCoordinationAsync(TransactionAccess access, CancellationToken cancellationToken, ulong? leaseId) =>
        TryCatch(operation: async () =>
    {
        ValidateTransactionCoordinationAccess(access: access, cancellationToken: cancellationToken, leaseId: leaseId);
        var acquiredLeaseId = ReserveAttempt(leaseId: leaseId);
        var writer = access == TransactionAccess.Write;
        var granted = false;

        try
        {
            if (writer)
            {
                await AcquireWriterAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                await AcquireReaderAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            granted = true;

            lock (state.Sync!)
            {
                state.LeaseKinds!.Add(key: acquiredLeaseId, value: writer);
                state.Pending--;
            }

            return acquiredLeaseId;
        }
        catch
        {
            if (granted)
            {
                ReleaseAccess(writer: writer);
            }

            lock (state.Sync!)
            {
                state.Pending--;
            }

            throw;
        }
    });

    private ulong ReserveAttempt(ulong? leaseId)
    {
        lock (state.Sync!)
        {
            ValidateOpenUnderLock();

            if (leaseId.HasValue && !state.LeaseKinds!.ContainsKey(key: leaseId.Value))
            {
                throw new ObjectDisposedException(objectName: "TransactionCoordinationLease");
            }

            var next = checked(state.NextId + 1);
            var pending = checked(state.Pending + 1);
            state.NextId = next;
            state.Pending = pending;
            return next;
        }
    }

    private async ValueTask AcquireWriterAsync(CancellationToken cancellationToken)
    {
        await broker.WaitAsync(semaphoreId: Turnstile, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await broker.WaitAsync(semaphoreId: Resource, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            broker.Release(semaphoreId: Turnstile);
            throw;
        }
    }

    private async ValueTask AcquireReaderAsync(CancellationToken cancellationToken)
    {
        await broker.WaitAsync(semaphoreId: Turnstile, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        broker.Release(semaphoreId: Turnstile);

        await broker.WaitAsync(semaphoreId: ReaderMutex, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (state.Readers == 0)
            {
                await broker.WaitAsync(semaphoreId: Resource, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            state.Readers = checked(state.Readers + 1);
        }
        finally
        {
            broker.Release(semaphoreId: ReaderMutex);
        }
    }

    public void CloseTransactionCoordination(ulong? leaseId) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionCoordinationLease(leaseId: leaseId);
        bool writer;

        lock (state.Sync!)
        {
            if (!leaseId.HasValue)
            {
                if (state.Closed)
                {
                    return;
                }

                ValidateClosableUnderLock();
                state.Closed = true;
                broker.Close();
                return;
            }

            if (!state.LeaseKinds!.Remove(key: leaseId.Value, value: out writer))
            {
                return;
            }

            state.Pending++;
        }

        try
        {
            ReleaseAccess(writer: writer);
        }
        finally
        {
            lock (state.Sync!)
            {
                state.Pending--;
            }
        }
    });

    private void ReleaseAccess(bool writer)
    {
        if (writer)
        {
            broker.Release(semaphoreId: Resource);
            broker.Release(semaphoreId: Turnstile);
            return;
        }

        broker.Wait(semaphoreId: ReaderMutex);

        try
        {
            state.Readers--;

            if (state.Readers == 0)
            {
                broker.Release(semaphoreId: Resource);
            }
        }
        finally
        {
            broker.Release(semaphoreId: ReaderMutex);
        }
    }
}
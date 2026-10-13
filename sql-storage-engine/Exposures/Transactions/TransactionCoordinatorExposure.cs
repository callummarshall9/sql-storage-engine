// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.TransactionCoordinations;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class TransactionCoordinatorExposure : IDisposable
{
    private readonly TransactionCoordinationService service;
    private readonly ulong? leaseId;
    public TransactionCoordinatorExposure()
    {
        service = new TransactionCoordinationService();
    }

    private TransactionCoordinatorExposure(TransactionCoordinationService service, TransactionCoordinationLeaseConfiguration configuration)
    {
        this.service = service;
        leaseId = configuration.LeaseId;
    }

    public async ValueTask<IDisposable> AcquireAsync(TransactionAccess access, CancellationToken cancellationToken = default) =>
        new TransactionCoordinatorExposure(service: service, configuration: new TransactionCoordinationLeaseConfiguration { LeaseId = await service.AcquireTransactionCoordinationAsync(access: access, cancellationToken: cancellationToken, leaseId: leaseId)
            .ConfigureAwait(continueOnCapturedContext: false) });

    public void Dispose() =>
        service.CloseTransactionCoordination(leaseId: leaseId);
}
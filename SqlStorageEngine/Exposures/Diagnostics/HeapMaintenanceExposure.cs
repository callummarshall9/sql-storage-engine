// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Services.Aggregations.HeapMaintenance;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class HeapMaintenanceExposure : ICompositionExposure, IDisposable
{
    private readonly HeapMaintenanceContextAggregationService service;
    private readonly MaintenanceContext state;
    public HeapMaintenanceExposure(ITableHeapExposure heap, IHeapMaintenanceCheckpointExposure checkpoint, ILockManagerExposure lockManager, TableId tableId, Func<CancellationToken, ValueTask>? throttle = null)
    {
        service = HeapMaintenanceContextAggregationService.CreateHeapMaintenanceContextAggregationService(tableHeapBinding: heap.ReadTableHeapBinding(), lockManagerBinding: lockManager.ReadLockManagerBinding());
        state = new MaintenanceContext
        {
            TableId = tableId,
            CheckpointSource = checkpoint,
            Read = checkpoint.ReadAsync,
            Write = (progress, token) => checkpoint.WriteAsync(heapMaintenanceProgress: progress, cancellationToken: token),
            Throttle = throttle
        };
    }

    public async Task<HeapMaintenanceProgress> RunBatchAsync(int maximumPages, CancellationToken cancellationToken = default) =>
        (await service.RunMaintenanceContextAsync(maintenanceContext: state with { MaximumPages = maximumPages }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Progress!;

    public void Dispose() =>
        service.CloseMaintenanceContext(maintenanceContext: state);
}
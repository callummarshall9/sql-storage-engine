// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Coordinations.TableHeaps;
using sql_storage_engine.Services.Coordinations.MaintenanceControl;

namespace sql_storage_engine.Services.Aggregations.HeapMaintenance;
internal sealed partial class HeapMaintenanceContextAggregationService(TableHeapContextCoordinationService heap, MaintenanceControlCoordinationService control) : IHeapMaintenanceContextAggregationService
{
    internal static HeapMaintenanceContextAggregationService CreateHeapMaintenanceContextAggregationService(TableHeapBinding tableHeapBinding, LockManagerBinding lockManagerBinding) =>
        new HeapMaintenanceContextAggregationService(heap: (TableHeapContextCoordinationService)tableHeapBinding.Service!, control: MaintenanceControlCoordinationService.CreateMaintenanceControlCoordinationService(lockManagerBinding: lockManagerBinding));

    public ValueTask<MaintenanceContext> RunMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        var state = await control.ReadMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        state.Pages = (await heap.ReadTableHeapContextPageIdsAsync(tableHeapContext: new TableHeapContext { MaximumPages = StorageLimits.MaximumScanPages }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds!;

        await control.AcquireMaintenanceContextAsync(maintenanceContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            state = control.CalculateMaintenanceContextEnd(maintenanceContext: state);

            for (var index = state.Progress!.NextPageIndex; index < state.End; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.Index = index;

                try
                {
                    var result = await heap.CompactTableHeapContextAsync(tableHeapContext: new TableHeapContext { PageId = state.Pages![index] }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    state.ReclaimedBytes = result.ReclaimedBytes;
                    state = control.ApplyMaintenanceContextSuccess(maintenanceContext: state);
                }
                catch when (!cancellationToken.IsCancellationRequested)
                {
                    state = control.ApplyMaintenanceContextFailure(maintenanceContext: state);
                }

                await control.WriteMaintenanceContextAsync(maintenanceContext: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

                await control.YieldMaintenanceContextAsync(maintenanceContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return state;
        }
        finally
        {
            control.ReleaseMaintenanceContext(maintenanceContext: state);
        }
    });

    public void CloseMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContextOnClose(maintenanceContext: maintenanceContext);
        control.CloseMaintenanceContext(maintenanceContext: maintenanceContext);
    });
}
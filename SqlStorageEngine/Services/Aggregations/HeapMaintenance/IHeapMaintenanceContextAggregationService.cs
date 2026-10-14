// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Aggregations.HeapMaintenance;
internal interface IHeapMaintenanceContextAggregationService
{
    ValueTask<MaintenanceContext> RunMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken);

    void CloseMaintenanceContext(MaintenanceContext maintenanceContext);
}
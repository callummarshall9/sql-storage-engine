// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Coordinations.MaintenanceControl;
internal interface IMaintenanceControlCoordinationService
{
    ValueTask<MaintenanceContext> ReadMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken);

    ValueTask AcquireMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken);

    void ReleaseMaintenanceContext(MaintenanceContext maintenanceContext);

    ValueTask WriteMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken);

    ValueTask YieldMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken);

    MaintenanceContext CalculateMaintenanceContextEnd(MaintenanceContext maintenanceContext);

    MaintenanceContext ApplyMaintenanceContextSuccess(MaintenanceContext maintenanceContext);

    MaintenanceContext ApplyMaintenanceContextFailure(MaintenanceContext maintenanceContext);

    void CloseMaintenanceContext(MaintenanceContext maintenanceContext);
}
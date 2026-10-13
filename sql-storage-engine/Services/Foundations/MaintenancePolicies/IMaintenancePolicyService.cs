// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.MaintenancePolicies;
internal interface IMaintenancePolicyService
{
    MaintenanceContext PrepareMaintenanceContext(MaintenanceContext maintenanceContext);

    MaintenanceContext CalculateMaintenanceContextEnd(MaintenanceContext maintenanceContext);

    MaintenanceContext ApplyMaintenanceContextSuccess(MaintenanceContext maintenanceContext);

    MaintenanceContext ApplyMaintenanceContextFailure(MaintenanceContext maintenanceContext);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.MaintenancePolicies;
internal sealed partial class MaintenancePolicyService : IMaintenancePolicyService
{
    public MaintenanceContext PrepareMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);

        return maintenanceContext with
        {
            Progress = maintenanceContext.Progress ?? new HeapMaintenanceProgress
            {
                NextPageIndex = 0,
                ReclaimedBytes = 0,
                FailureCount = 0,
                Complete = false
            }
        };
    });

    public MaintenanceContext CalculateMaintenanceContextEnd(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);

        return maintenanceContext with
        {
            End = Math.Min(val1: maintenanceContext.Pages!.Count, val2: checked(maintenanceContext.Progress!.NextPageIndex + maintenanceContext.MaximumPages))
        };
    });

    public MaintenanceContext ApplyMaintenanceContextSuccess(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);

        return maintenanceContext with
        {
            Progress = maintenanceContext.Progress!with
            {
                NextPageIndex = maintenanceContext.Index + 1,
                ReclaimedBytes = checked(maintenanceContext.Progress.ReclaimedBytes + maintenanceContext.ReclaimedBytes),
                Complete = maintenanceContext.Index + 1 == maintenanceContext.Pages!.Count
            }
        };
    });

    public MaintenanceContext ApplyMaintenanceContextFailure(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);

        return maintenanceContext with
        {
            Progress = maintenanceContext.Progress!with
            {
                NextPageIndex = maintenanceContext.Index + 1,
                FailureCount = maintenanceContext.Progress.FailureCount + 1
            }
        };
    });
}
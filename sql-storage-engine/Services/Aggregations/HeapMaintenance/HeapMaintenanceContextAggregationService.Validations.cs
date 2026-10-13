// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Aggregations.HeapMaintenance;
internal sealed partial class HeapMaintenanceContextAggregationService
{
    private static void ValidateMaintenanceContext(MaintenanceContext maintenanceContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(maintenanceContext is null, nameof(maintenanceContext)), (maintenanceContext?.MaximumPages <= 0, nameof(MaintenanceContext.MaximumPages))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateMaintenanceContextOnClose(MaintenanceContext maintenanceContext) =>
        Validate(inputs: [(maintenanceContext is null, nameof(maintenanceContext))]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A maintenance context is required.", paramName: input.ParameterName);
            }
        }
    }
}
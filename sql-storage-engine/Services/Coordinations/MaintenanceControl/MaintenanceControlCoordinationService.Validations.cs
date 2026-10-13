// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Coordinations.MaintenanceControl;
internal sealed partial class MaintenanceControlCoordinationService
{
    private static void ValidateMaintenanceContext(MaintenanceContext maintenanceContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(maintenanceContext is null, nameof(maintenanceContext))]);
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
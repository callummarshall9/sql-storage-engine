// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Orchestrations.GraphRows;
internal sealed partial class GraphRowOrchestrationService
{
    private static void ValidateGraph(GraphRowContext graphRowContext)
    {
        ArgumentNullException.ThrowIfNull(argument: graphRowContext);
        ArgumentNullException.ThrowIfNull(argument: graphRowContext.Operation);

        if (graphRowContext.Operation is "Encode" or "Decode" or "Prepare")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Identity);
        }
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Processings.GraphRowProjections;
internal sealed partial class GraphRowProjectionProcessingService
{
    private static void ValidateProjection(GraphRowContext graphRowContext)
    {
        ArgumentNullException.ThrowIfNull(argument: graphRowContext);
        ArgumentNullException.ThrowIfNull(argument: graphRowContext.Operation);

        if (graphRowContext.Operation == "Options")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Options);

            if (graphRowContext.Options.MaximumEdges is <= 0 or > GraphTraversalOptions.MaximumSupportedEdges)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(graphRowContext.Options.MaximumEdges), message: $"A graph traversal limit must be from 1 through {GraphTraversalOptions.MaximumSupportedEdges} edges.");
            }
        }

        if (graphRowContext.Operation is "Append" or "Payload" or "ValidatePayload")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Row);
        }

        if (graphRowContext.Operation == "Append")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Generated);
        }

        if (graphRowContext.Operation is "ValidatePayload" or "ValidateNode" or "ValidateEdge")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Table);
        }

        if (graphRowContext.Operation == "ValidateUpdate")
        {
            ArgumentNullException.ThrowIfNull(argument: graphRowContext.Update);
        }

        if (graphRowContext.GeneratedCount < 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(graphRowContext.GeneratedCount));
        }
    }
}
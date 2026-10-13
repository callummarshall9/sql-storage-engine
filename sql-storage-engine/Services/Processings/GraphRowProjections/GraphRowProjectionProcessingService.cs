// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.GraphRowProjections;
internal sealed partial class GraphRowProjectionProcessingService : IGraphRowProjectionProcessingService
{
    public GraphRowContext ProcessGraphRowContext(GraphRowContext graphRowContext) =>
        TryCatch(operation: () =>
    {
        ValidateProjection(graphRowContext: graphRowContext);

        switch (graphRowContext.Operation)
        {
            case "Append":
                graphRowContext.Row = new Row
                {
                    Values = Array.AsReadOnly(array: graphRowContext.Row!.Values.Concat(second: graphRowContext.Generated!)
            .ToArray())
                };
                break;
            case "Payload":
                graphRowContext.Row = new Row
                {
                    Values = Array.AsReadOnly(array: graphRowContext.Row!.Values.Take(count: graphRowContext.Row.Values.Count - graphRowContext.GeneratedCount)
            .ToArray())
                };
                break;
            case "ValidatePayload":
                if (graphRowContext.Row!.Values.Count != graphRowContext.Table!.Columns.Count - graphRowContext.GeneratedCount)
                {
                    throw new ArgumentException(message: $"The graph payload requires exactly {graphRowContext.Table.Columns.Count - graphRowContext.GeneratedCount} values.", paramName: graphRowContext.ParameterName);
                }

                break;
            case "ValidateUpdate":
                if (graphRowContext.Update!.Columns.Any(predicate: column => column.ColumnIndex < 0 || column.ColumnIndex >= graphRowContext.PayloadColumnCount))
                {
                    throw new ArgumentException(message: "Graph identity and endpoint columns cannot be updated as payload.", paramName: graphRowContext.ParameterName);
                }

                break;
            case "ValidateNode":
                if (graphRowContext.NodeId.DatabaseId != graphRowContext.DatabaseId || graphRowContext.NodeId.TableId != graphRowContext.Table!.Id || graphRowContext.NodeId.SchemaVersion != graphRowContext.Table.SchemaVersion)
                {
                    throw new ArgumentException(message: "The graph node identity does not belong to the current database/table/schema.", paramName: graphRowContext.ParameterName);
                }

                break;
            case "ValidateEdge":
                if (graphRowContext.EdgeId.DatabaseId != graphRowContext.DatabaseId || graphRowContext.EdgeId.TableId != graphRowContext.Table!.Id || graphRowContext.EdgeId.SchemaVersion != graphRowContext.Table.SchemaVersion)
                {
                    throw new ArgumentException(message: "The graph edge identity does not belong to the current database/table/schema.", paramName: graphRowContext.ParameterName);
                }

                break;
        }

        return graphRowContext;
    });
}
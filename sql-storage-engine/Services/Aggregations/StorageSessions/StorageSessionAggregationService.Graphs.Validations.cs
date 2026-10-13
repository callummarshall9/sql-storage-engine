// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private static void ValidateGraphRequest(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Graph);
        var graph = storageSessionContext.Graph;
        ArgumentNullException.ThrowIfNull(argument: graph.Operation);
        ArgumentNullException.ThrowIfNull(argument: graph.Handle);
        ArgumentNullException.ThrowIfNull(argument: graph.Handle.Engine);
        cancellationToken.ThrowIfCancellationRequested();

        if (graph.Operation is "EdgeInsert" or "EdgeReconnect" or "EdgeUpdateReconnect")
        {
            ArgumentNullException.ThrowIfNull(argument: graph.Endpoints);
        }

        if (graph.Operation == "Create" && graph.Kind == 2)
        {
            ArgumentNullException.ThrowIfNull(argument: graph.TableEndpoints);
        }

        if (graph.Operation == "Create")
        {
            ArgumentNullException.ThrowIfNull(argument: graph.Name);
            ArgumentNullException.ThrowIfNull(argument: graph.PayloadColumns);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(argument: graph.Storage);
        }

        if (graph.Operation is "NodeInsert" or "EdgeInsert")
        {
            ArgumentNullException.ThrowIfNull(argument: graph.Row);
        }

        if (graph.Operation is "NodeUpdate" or "EdgeUpdate" or "EdgeUpdateReconnect")
        {
            ArgumentNullException.ThrowIfNull(argument: graph.Update);
        }
    }
}
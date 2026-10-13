// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Services.Processings.GraphRowProjections;

namespace sql_storage_engine.Exposures.Graph;
public static class GraphTraversalOptionsExposure
{
    private static readonly GraphRowProjectionProcessingService service = new();
    public static GraphTraversalOptions CreateGraphTraversalOptions(int maximumEdges = 10_000) =>
        service.ProcessGraphRowContext(graphRowContext: new GraphRowContext { Operation = "Options", Options = new GraphTraversalOptions { MaximumEdges = maximumEdges } })
            .Options!;
}
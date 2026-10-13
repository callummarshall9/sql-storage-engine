// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Services.Processings.GraphIdentityValues;
using sql_storage_engine.Services.Processings.GraphRowProjections;

namespace sql_storage_engine.Services.Orchestrations.GraphRows;
internal sealed partial class GraphRowOrchestrationService : IGraphRowOrchestrationService
{
    private readonly GraphIdentityValueProcessingService identities;
    private readonly GraphRowProjectionProcessingService projections;
    private GraphRowOrchestrationService(GraphIdentityValueProcessingService identities, GraphRowProjectionProcessingService projections)
    {
        this.identities = identities;
        this.projections = projections;
    }

    internal static GraphRowOrchestrationService CreateGraphRowOrchestrationService() =>
        new(identities: GraphIdentityValueProcessingService.CreateGraphIdentityValueProcessingService(), projections: new GraphRowProjectionProcessingService());

    public GraphRowContext ProcessGraphRowContext(GraphRowContext graphRowContext) =>
        TryCatch(operation: () =>
    {
        ValidateGraph(graphRowContext: graphRowContext);

        switch (graphRowContext.Operation)
        {
            case "Prepare":
                graphRowContext.Identity = identities.PrepareGraphIdentityValue(graphIdentityValue: graphRowContext.Identity!);
                break;
            case "Encode":
                graphRowContext.Identity = identities.EncodeGraphIdentityValue(graphIdentityValue: graphRowContext.Identity!);
                break;
            case "Decode":
                graphRowContext.Identity = identities.DecodeGraphIdentityValue(graphIdentityValue: graphRowContext.Identity!);
                break;
            default:
                return projections.ProcessGraphRowContext(graphRowContext: graphRowContext);
        }

        return graphRowContext;
    });
}
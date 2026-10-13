// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Linq;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.GraphIdentities;

namespace sql_storage_engine.Services.Processings.GraphIdentityValues;
internal sealed partial class GraphIdentityValueProcessingService(GraphIdentityService identities) : IGraphIdentityValueProcessingService
{
    internal static GraphIdentityValueProcessingService CreateGraphIdentityValueProcessingService() =>
        new(identities: new GraphIdentityService());

    public GraphIdentityValue PrepareGraphIdentityValue(GraphIdentityValue graphIdentityValue) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityValue(graphIdentityValue: graphIdentityValue);
        var identity = identities.EncodeGraphIdentity(graphIdentity: graphIdentityValue.Identity!);
        return BindIdentity(graphIdentityValue: graphIdentityValue, identity: identity);
    });

    public GraphIdentityValue EncodeGraphIdentityValue(GraphIdentityValue graphIdentityValue) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityValue(graphIdentityValue: graphIdentityValue);
        var identity = identities.EncodeGraphIdentity(graphIdentity: graphIdentityValue.Identity!);

        return graphIdentityValue with
        {
            Value = new BinarySqlValue
            {
                Content = new string (identity.Payload.Select(selector: item => (char)item)
            .ToArray()),
                StorageFamily = SqlStorageFamily.Binary
            }
        };
    });

    public GraphIdentityValue DecodeGraphIdentityValue(GraphIdentityValue graphIdentityValue) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityValueInput(graphIdentityValue: graphIdentityValue);

        var bytes = graphIdentityValue.Value is BinarySqlValue binary ? binary.Content.Select(selector: item => checked((byte)item))
            .ToArray() : null;

        var identity = identities.DecodeGraphIdentity(graphIdentity: graphIdentityValue.Identity!with { Payload = bytes! });
        return BindIdentity(graphIdentityValue: graphIdentityValue, identity: identity);
    });

    public GraphIdentityValue FormatGraphIdentityValue(GraphIdentityValue graphIdentityValue) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityValue(graphIdentityValue: graphIdentityValue);
        var identity = graphIdentityValue.Identity!;

        return graphIdentityValue with
        {
            Text = $"graph-{(identity.Kind == 1 ? "node" : "edge")}:{identity.TableId}/{identity.Value:D}"};
    });

    private static GraphIdentityValue BindIdentity(GraphIdentityValue graphIdentityValue, GraphIdentity identity) =>
        graphIdentityValue with
    {
        NodeId = new GraphNodeId
        {
            DatabaseId = new DatabaseId
            {
                Value = identity.DatabaseId
            },
            TableId = new TableId
            {
                Value = identity.TableId
            },
            SchemaVersion = identity.SchemaVersion,
            Value = identity.Value
        },
        EdgeId = new GraphEdgeId
        {
            DatabaseId = new DatabaseId
            {
                Value = identity.DatabaseId
            },
            TableId = new TableId
            {
                Value = identity.TableId
            },
            SchemaVersion = identity.SchemaVersion,
            Value = identity.Value
        }
    };
}
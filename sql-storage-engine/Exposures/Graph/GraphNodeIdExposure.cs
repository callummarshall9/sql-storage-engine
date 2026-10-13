// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.GraphIdentityValues;
using sql_storage_engine.Services.Foundations.GraphIdentities;

namespace sql_storage_engine.Exposures.Graph;
public static class GraphNodeIdExposure
{
    public const int EncodedLength = 50;
    private static readonly IGraphIdentityValueProcessingService service = new GraphIdentityValueProcessingService(identities: new GraphIdentityService());
    public static GraphNodeId CreateGraphNodeId(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value) =>
        service.PrepareGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 1, DatabaseId = databaseId.Value, TableId = tableId.Value, SchemaVersion = schemaVersion, Value = value } })
            .NodeId;

    public static SqlValue ToSqlValue(GraphNodeId graphNodeId) =>
        service.EncodeGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 1, DatabaseId = graphNodeId.DatabaseId.Value, TableId = graphNodeId.TableId.Value, SchemaVersion = graphNodeId.SchemaVersion, Value = graphNodeId.Value } })
            .Value!;

    public static GraphNodeId FromSqlValue(SqlValue sqlValue) =>
        service.DecodeGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Value = sqlValue, Identity = new GraphIdentity { Kind = 1, ParameterName = "value" } })
            .NodeId;

    public static string FormatGraphNodeId(GraphNodeId graphNodeId) =>
        service.FormatGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 1, TableId = graphNodeId.TableId.Value, Value = graphNodeId.Value } })
            .Text!;
}
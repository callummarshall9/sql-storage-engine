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
public static class GraphEdgeIdExposure
{
    public const int EncodedLength = 50;
    private static readonly IGraphIdentityValueProcessingService service = new GraphIdentityValueProcessingService(identities: new GraphIdentityService());
    public static GraphEdgeId CreateGraphEdgeId(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value) =>
        service.PrepareGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 2, DatabaseId = databaseId.Value, TableId = tableId.Value, SchemaVersion = schemaVersion, Value = value } })
            .EdgeId;

    public static SqlValue ToSqlValue(GraphEdgeId graphEdgeId) =>
        service.EncodeGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 2, DatabaseId = graphEdgeId.DatabaseId.Value, TableId = graphEdgeId.TableId.Value, SchemaVersion = graphEdgeId.SchemaVersion, Value = graphEdgeId.Value } })
            .Value!;

    public static GraphEdgeId FromSqlValue(SqlValue sqlValue) =>
        service.DecodeGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Value = sqlValue, Identity = new GraphIdentity { Kind = 2, ParameterName = "value" } })
            .EdgeId;

    public static string FormatGraphEdgeId(GraphEdgeId graphEdgeId) =>
        service.FormatGraphIdentityValue(graphIdentityValue: new GraphIdentityValue { Identity = new GraphIdentity { Kind = 2, TableId = graphEdgeId.TableId.Value, Value = graphEdgeId.Value } })
            .Text!;
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Services.Foundations.GraphIdentities;

namespace sql_storage_engine.Exposures.Graph;
internal static class GraphIdentityExposure
{
    private static readonly IGraphIdentityService service = new GraphIdentityService();
    public static byte[] EncodeGraphIdentity(byte kind, Guid databaseId, ulong tableId, ulong schemaVersion, Guid value) =>
        service.EncodeGraphIdentity(graphIdentity: new GraphIdentity { Kind = kind, DatabaseId = databaseId, TableId = tableId, SchemaVersion = schemaVersion, Value = value })
            .Payload;

    public static GraphIdentity DecodeGraphIdentity(byte[] payload, byte expectedKind, string parameterName) =>
        service.DecodeGraphIdentity(graphIdentity: new GraphIdentity { Kind = expectedKind, Payload = payload, ParameterName = parameterName });
}
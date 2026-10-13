// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Foundations.GraphIdentities;
internal sealed partial class GraphIdentityService : IGraphIdentityService
{
    public GraphIdentity EncodeGraphIdentity(GraphIdentity graphIdentity) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityOwnership(graphIdentity: graphIdentity);
        var bytes = new byte[50];
        bytes[0] = 1;
        bytes[1] = graphIdentity.Kind;
        graphIdentity.DatabaseId.TryWriteBytes(destination: bytes.AsSpan(start: 2, length: 16));
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 18, length: 8), value: graphIdentity.TableId);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 26, length: 8), value: graphIdentity.SchemaVersion);
        graphIdentity.Value.TryWriteBytes(destination: bytes.AsSpan(start: 34, length: 16));

        return graphIdentity with
        {
            Payload = bytes
        };
    });

    public GraphIdentity DecodeGraphIdentity(GraphIdentity graphIdentity) =>
        TryCatch(operation: () =>
    {
        ValidateGraphIdentityPayload(graphIdentity: graphIdentity);
        var bytes = graphIdentity.Payload;

        var decoded = new GraphIdentity
        {
            Kind = graphIdentity.Kind,
            DatabaseId = new Guid(bytes.AsSpan(start: 2, length: 16)),
            TableId = BinaryPrimitives.ReadUInt64LittleEndian(source: bytes.AsSpan(start: 18, length: 8)),
            SchemaVersion = BinaryPrimitives.ReadUInt64LittleEndian(source: bytes.AsSpan(start: 26, length: 8)),
            Value = new Guid(bytes.AsSpan(start: 34, length: 16))
        };

        try
        {
            ValidateGraphIdentityOwnership(graphIdentity: decoded);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(message: "The graph identity contains invalid ownership metadata.", paramName: graphIdentity.ParameterName, innerException: exception);
        }

        return decoded;
    });
}
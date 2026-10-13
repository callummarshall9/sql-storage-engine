// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Foundations.GraphIdentities;
internal sealed partial class GraphIdentityService
{
    private static void ValidateGraphIdentityOwnership(GraphIdentity graphIdentity)
    {
        ArgumentNullException.ThrowIfNull(argument: graphIdentity);

        if (graphIdentity.DatabaseId == Guid.Empty)
        {
            throw new ArgumentException(message: "A graph identity requires a database ID.", paramName: "databaseId");
        }

        if (graphIdentity.TableId == 0)
        {
            throw new ArgumentException(message: "A graph identity requires a table ID.", paramName: "tableId");
        }

        if (graphIdentity.SchemaVersion == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: "schemaVersion");
        }

        if (graphIdentity.Value == Guid.Empty)
        {
            throw new ArgumentException(message: "A graph identity value cannot be empty.", paramName: "value");
        }
    }

    private static void ValidateGraphIdentityPayload(GraphIdentity graphIdentity)
    {
        ArgumentNullException.ThrowIfNull(argument: graphIdentity);

        if (graphIdentity.Payload is null || graphIdentity.Payload.Length != 50)
        {
            throw new ArgumentException(message: "A graph identity must be binary(50).", paramName: graphIdentity.ParameterName);
        }

        if (graphIdentity.Payload[0] != 1 || graphIdentity.Payload[1] != graphIdentity.Kind)
        {
            throw new ArgumentException(message: "The graph identity has an unsupported format or entity kind.", paramName: graphIdentity.ParameterName);
        }
    }
}
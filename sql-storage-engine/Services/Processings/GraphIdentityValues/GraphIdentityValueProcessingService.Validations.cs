// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Services.Processings.GraphIdentityValues;
internal sealed partial class GraphIdentityValueProcessingService
{
    private static void ValidateGraphIdentityValue(GraphIdentityValue graphIdentityValue) =>
        Validate(inputs: [(graphIdentityValue is null, nameof(graphIdentityValue)), (graphIdentityValue?.Identity is null, nameof(GraphIdentityValue.Identity))]);

    private static void ValidateGraphIdentityValueInput(GraphIdentityValue graphIdentityValue) =>
        Validate(inputs: [(graphIdentityValue is null, nameof(graphIdentityValue)), (graphIdentityValue?.Identity is null, nameof(GraphIdentityValue.Identity)), (graphIdentityValue?.Value is null, "value")]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.FuzzCoreRegistries;
internal sealed partial class FuzzCoreRegistryOrchestrationService
{
    private static void ValidateFuzzCoreRegistry(FuzzCoreRegistry fuzzCoreRegistry)
    {
        Validate(inputs: [(fuzzCoreRegistry is null, nameof(fuzzCoreRegistry)), (fuzzCoreRegistry?.Decoders is null || fuzzCoreRegistry.Decoders.Length != 10, nameof(FuzzCoreRegistry.Decoders))]);

        foreach (var decoder in fuzzCoreRegistry!.Decoders!)
        {
            Validate(inputs: [(decoder is null, nameof(FuzzCoreRegistry.Decoders))]);
        }
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Valid bounded fuzz inputs are required.", paramName: input.ParameterName);
            }
        }
    }
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.FuzzManifests;
internal sealed partial class FuzzManifestService
{
    private static void ValidateFuzzManifest(ReadOnlyMemory<byte> source) =>
        Validate(inputs: [(source.Length > 16 * 1024 * 1024, nameof(source))]);

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
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Processings.IdentifierDisplays;
internal sealed partial class IdentifierDisplayProcessingService
{
    private static void ValidateIdentifierDisplay(IdentifierDisplay identifierDisplay)
    {
        ArgumentNullException.ThrowIfNull(argument: identifierDisplay);
        Validate(inputs: [(!Enum.IsDefined(value: identifierDisplay.Kind), nameof(identifierDisplay.Kind))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }
}
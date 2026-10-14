// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.LockRules;
internal sealed partial class LockRulesProcessingService
{
    private static void ValidateModes(LockMode first, LockMode second) =>
        Validate(inputs: [(!Enum.IsDefined(value: first), nameof(first)), (!Enum.IsDefined(value: second), nameof(second))]);

    private static void ValidateConversionModes(LockMode current, LockMode requested) =>
        Validate(inputs: [(!Enum.IsDefined(value: current), nameof(current)), (!Enum.IsDefined(value: requested), nameof(requested))]);

    private static void ValidateConversion(LockMode current, LockMode requested)
    {
        if (!CanConvertCore(current: current, requested: requested))
        {
            throw new InvalidOperationException(message: $"A {current} lock cannot be converted to {requested}.");
        }
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
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
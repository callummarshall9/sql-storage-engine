// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Orchestrations.StorageResources;
internal sealed partial class StorageResourceOrchestrationService
{
    private static void ValidateResourceLimitRequestOnPrepare(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.Limits is null, nameof(resourceLimitRequest.Limits))]);
    }

    private static void ValidateResourceLimitRequestOnGet(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.Budget is null, nameof(resourceLimitRequest.Budget))]);
    }

    private static void ValidateResourceLimitRequestOnRelease(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest.Reservation);
        Validate(inputs: [(resourceLimitRequest.Budget is null, nameof(resourceLimitRequest.Budget)), (resourceLimitRequest.Reservation.Request is null, nameof(resourceLimitRequest.Reservation))]);
        ValidateOwnership(inputs: [(!ReferenceEquals(objA: resourceLimitRequest.Budget, objB: resourceLimitRequest.Reservation.Request!.Budget), nameof(resourceLimitRequest.Reservation))]);
    }

    private static void Validate(ReadOnlySpan<(bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }

    private static void ValidateOwnership(ReadOnlySpan<(bool WrongOwner, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.WrongOwner)
            {
                throw new ArgumentException(message: "Reservation belongs to another resource limiter.", paramName: input.ParameterName);
            }
        }
    }
}
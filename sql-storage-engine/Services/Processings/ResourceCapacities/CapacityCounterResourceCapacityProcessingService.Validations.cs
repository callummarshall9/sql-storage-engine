// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Resources;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.ResourceCapacities;
internal sealed partial class CapacityCounterResourceCapacityProcessingService
{
    private static void ValidateResourceLimitRequestOnAcquire(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.Budget is null, true, nameof(resourceLimitRequest.Budget)), (resourceLimitRequest.Kind is not (ResourceLimitKind.Transactions or ResourceLimitKind.Pins), false, nameof(resourceLimitRequest.Kind)), (resourceLimitRequest.ReservationCounter is not null || resourceLimitRequest.Released != 0, false, nameof(resourceLimitRequest.ReservationCounter))]);
    }

    private static void ValidateResourceLimitRequestOnRelease(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.ReservationCounter is null, true, nameof(resourceLimitRequest.ReservationCounter))]);
    }

    private static void ValidateResourceLimitRequestOnReadActive(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.Budget is null, true, nameof(resourceLimitRequest.Budget)), (resourceLimitRequest.Kind is not (ResourceLimitKind.Transactions or ResourceLimitKind.Pins), false, nameof(resourceLimitRequest.Kind))]);
    }

    private static void ValidateResourceLimitRequestOnCheck(ResourceLimitRequest resourceLimitRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: resourceLimitRequest);
        Validate(inputs: [(resourceLimitRequest.Budget is null, true, nameof(resourceLimitRequest.Budget)), (resourceLimitRequest.Kind is not (ResourceLimitKind.RowBytes or ResourceLimitKind.KeyBytes or ResourceLimitKind.ValueBytes or ResourceLimitKind.OverflowPages or ResourceLimitKind.UndoBytes or ResourceLimitKind.ScanPages), false, nameof(resourceLimitRequest.Kind))]);
    }

    private static void ValidateResourceQuota(ResourceLimitRequest resourceLimitRequest)
    {
        var limits = resourceLimitRequest.Budget!.Limits;

        var maximum = resourceLimitRequest.Kind switch
        {
            ResourceLimitKind.RowBytes => limits.RowBytes,
            ResourceLimitKind.KeyBytes => limits.KeyBytes,
            ResourceLimitKind.ValueBytes => limits.ValueBytes,
            ResourceLimitKind.OverflowPages => limits.OverflowPages,
            ResourceLimitKind.UndoBytes => limits.UndoBytes,
            ResourceLimitKind.ScanPages => limits.ScanPages,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(resourceLimitRequest.Kind))};

        Validate(inputs: [(resourceLimitRequest.Value < 0 || resourceLimitRequest.Value > maximum, $"{GetResourceCode(kind: resourceLimitRequest.Kind)} limit {maximum} exceeded by {resourceLimitRequest.Value}.")]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, bool Missing, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "Invalid resource request.", paramName: input.ParameterName);
        }
    }

    private static void Validate(ReadOnlySpan<(bool Exceeded, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Exceeded)
            {
                throw new StorageResourceExhaustedException(message: input.Message);
            }
        }
    }
}
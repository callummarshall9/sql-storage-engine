// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Foundations.CapacityCounters;
internal sealed partial class CapacityCounterService
{
    private static void ValidateCapacityCounterOnAcquire(CapacityCounter capacityCounter)
    {
        ArgumentNullException.ThrowIfNull(argument: capacityCounter);
        Validate(inputs: [(capacityCounter.Maximum <= 0, nameof(capacityCounter.Maximum)), (Volatile.Read(location: ref capacityCounter.Count) < 0, nameof(capacityCounter.Count))]);
    }

    private static void ValidateCapacityCounterOnRelease(CapacityCounter capacityCounter)
    {
        ArgumentNullException.ThrowIfNull(argument: capacityCounter);
        Validate(inputs: [(capacityCounter.Maximum <= 0, nameof(capacityCounter.Maximum)), (Volatile.Read(location: ref capacityCounter.Count) <= 0, nameof(capacityCounter.Count))]);
    }

    private static void ValidateCapacityCounterCountOnGet(CapacityCounter capacityCounter)
    {
        ArgumentNullException.ThrowIfNull(argument: capacityCounter);
        Validate(inputs: [(capacityCounter.Maximum <= 0, nameof(capacityCounter.Maximum)), (Volatile.Read(location: ref capacityCounter.Count) < 0, nameof(capacityCounter.Count))]);
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
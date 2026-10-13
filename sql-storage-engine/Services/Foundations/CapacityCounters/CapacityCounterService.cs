// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Foundations.CapacityCounters;
/// <summary>Applies linearizable in-memory capacity accounting without temporary over-admission.</summary>
internal sealed partial class CapacityCounterService : ICapacityCounterService
{
    public bool TryAcquireCapacityCounter(CapacityCounter capacityCounter) =>
        TryCatch(operation: () =>
    {
        ValidateCapacityCounterOnAcquire(capacityCounter: capacityCounter);

        while (true)
        {
            var current = Volatile.Read(location: ref capacityCounter.Count);

            if (current >= capacityCounter.Maximum)
            {
                return false;
            }

            if (Interlocked.CompareExchange(location1: ref capacityCounter.Count, value: current + 1, comparand: current) == current)
            {
                return true;
            }
        }
    });

    public void ReleaseCapacityCounter(CapacityCounter capacityCounter) =>
        TryCatch(operation: () =>
    {
        ValidateCapacityCounterOnRelease(capacityCounter: capacityCounter);
        Interlocked.Decrement(location: ref capacityCounter.Count);
    });

    public int GetCapacityCounterCount(CapacityCounter capacityCounter) =>
        TryCatch(operation: () =>
    {
        ValidateCapacityCounterCountOnGet(capacityCounter: capacityCounter);
        return Volatile.Read(location: ref capacityCounter.Count);
    });
}
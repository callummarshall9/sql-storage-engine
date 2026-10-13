// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Foundations.CapacityCounters;
internal interface ICapacityCounterService
{
    bool TryAcquireCapacityCounter(CapacityCounter capacityCounter);

    void ReleaseCapacityCounter(CapacityCounter capacityCounter);

    int GetCapacityCounterCount(CapacityCounter capacityCounter);
}
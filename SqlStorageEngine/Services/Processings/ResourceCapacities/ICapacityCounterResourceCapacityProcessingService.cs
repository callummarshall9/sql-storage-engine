// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Processings.ResourceCapacities;
internal interface ICapacityCounterResourceCapacityProcessingService
{
    ResourceLimitRequest AcquireResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    void ReleaseResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    void CheckResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    int ReadActiveResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Resources;

namespace sql_storage_engine.Services.Orchestrations.StorageResources;
internal interface IStorageResourceOrchestrationService
{
    ResourceLimitRequest PrepareResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    ResourceLimitRequest GetResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    ResourceLimitRequest AcquireResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    void ReleaseResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    void CheckResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);

    int ReadActiveResourceLimitRequest(ResourceLimitRequest resourceLimitRequest);
}
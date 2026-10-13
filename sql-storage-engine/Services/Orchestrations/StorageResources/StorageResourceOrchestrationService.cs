// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Resources;
using sql_storage_engine.Services.Processings.ResourceCapacities;
using sql_storage_engine.Services.Processings.StorageLimitSettings;

namespace sql_storage_engine.Services.Orchestrations.StorageResources;
internal sealed partial class StorageResourceOrchestrationService(IStorageLimitsProcessingService limits, ICapacityCounterResourceCapacityProcessingService capacities) : IStorageResourceOrchestrationService
{
    public ResourceLimitRequest PrepareResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnPrepare(resourceLimitRequest: resourceLimitRequest);
        var preparedLimits = limits.PrepareStorageLimits(storageLimits: resourceLimitRequest.Limits!);

        return new ResourceLimitRequest
        {
            Limits = preparedLimits,
            Budget = new ResourceBudget
            {
                Limits = preparedLimits,
                Transactions = new CapacityCounter
                {
                    Maximum = preparedLimits.ConcurrentTransactions
                },
                Pins = new CapacityCounter
                {
                    Maximum = preparedLimits.PinsPerTransaction
                }
            }
        };
    });

    public ResourceLimitRequest GetResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnGet(resourceLimitRequest: resourceLimitRequest);

        return new ResourceLimitRequest
        {
            Budget = resourceLimitRequest.Budget,
            Limits = resourceLimitRequest.Budget!.Limits
        };
    });

    public ResourceLimitRequest AcquireResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnGet(resourceLimitRequest: resourceLimitRequest);
        return capacities.AcquireResourceLimitRequest(resourceLimitRequest: resourceLimitRequest);
    });

    public void ReleaseResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnRelease(resourceLimitRequest: resourceLimitRequest);
        capacities.ReleaseResourceLimitRequest(resourceLimitRequest: resourceLimitRequest.Reservation!.Request!);
    });

    public void CheckResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnGet(resourceLimitRequest: resourceLimitRequest);
        capacities.CheckResourceLimitRequest(resourceLimitRequest: resourceLimitRequest);
    });

    public int ReadActiveResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnGet(resourceLimitRequest: resourceLimitRequest);
        return capacities.ReadActiveResourceLimitRequest(resourceLimitRequest: resourceLimitRequest);
    });
}
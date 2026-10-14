// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using sql_storage_engine.Models.Resources;
using sql_storage_engine.Services.Foundations.CapacityCounters;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.ResourceCapacities;
internal sealed partial class CapacityCounterResourceCapacityProcessingService(ICapacityCounterService capacities) : ICapacityCounterResourceCapacityProcessingService
{
    public ResourceLimitRequest AcquireResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnAcquire(resourceLimitRequest: resourceLimitRequest);
        var counter = SelectCapacityCounter(resourceLimitRequest: resourceLimitRequest);

        if (!capacities.TryAcquireCapacityCounter(capacityCounter: counter))
        {
            throw new StorageResourceExhaustedException(message: $"{GetResourceCode(kind: resourceLimitRequest.Kind)} limit {counter.Maximum} exceeded.");
        }

        resourceLimitRequest.ReservationCounter = counter;
        return resourceLimitRequest;
    });

    public void ReleaseResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnRelease(resourceLimitRequest: resourceLimitRequest);

        if (Interlocked.Exchange(location1: ref resourceLimitRequest.Released, value: 1) == 0)
        {
            capacities.ReleaseCapacityCounter(capacityCounter: resourceLimitRequest.ReservationCounter!);
        }
    });

    public void CheckResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnCheck(resourceLimitRequest: resourceLimitRequest);
        ValidateResourceQuota(resourceLimitRequest: resourceLimitRequest);
    });

    public int ReadActiveResourceLimitRequest(ResourceLimitRequest resourceLimitRequest) =>
        TryCatch(operation: () =>
    {
        ValidateResourceLimitRequestOnReadActive(resourceLimitRequest: resourceLimitRequest);
        return capacities.GetCapacityCounterCount(capacityCounter: SelectCapacityCounter(resourceLimitRequest: resourceLimitRequest));
    });

    private static CapacityCounter SelectCapacityCounter(ResourceLimitRequest resourceLimitRequest) =>
        resourceLimitRequest.Kind == ResourceLimitKind.Transactions ? resourceLimitRequest.Budget!.Transactions : resourceLimitRequest.Budget!.Pins;

    private static string GetResourceCode(ResourceLimitKind kind) =>
        kind switch
    {
        ResourceLimitKind.Transactions => "CONCURRENT_TRANSACTIONS",
        ResourceLimitKind.Pins => "TRANSACTION_PINS",
        ResourceLimitKind.RowBytes => "ROW_BYTES",
        ResourceLimitKind.KeyBytes => "KEY_BYTES",
        ResourceLimitKind.ValueBytes => "VALUE_BYTES",
        ResourceLimitKind.OverflowPages => "OVERFLOW_PAGES",
        ResourceLimitKind.UndoBytes => "UNDO_BYTES",
        ResourceLimitKind.ScanPages => "SCAN_PAGES",
        _ => throw new System.ArgumentOutOfRangeException(paramName: nameof(kind))};
}
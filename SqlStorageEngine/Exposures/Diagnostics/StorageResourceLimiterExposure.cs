// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Resources;
using sql_storage_engine.Services.Foundations.CapacityCounters;
using sql_storage_engine.Services.Orchestrations.StorageResources;
using sql_storage_engine.Services.Processings.ResourceCapacities;
using sql_storage_engine.Services.Processings.StorageLimitSettings;

namespace sql_storage_engine.Exposures.Diagnostics;
/// <summary>Validates file-controlled sizes and owns bounded transaction and pin reservations.</summary>
public sealed class StorageResourceLimiterExposure
{
    private static readonly IStorageResourceOrchestrationService service = new StorageResourceOrchestrationService(limits: new StorageLimitsProcessingService(), capacities: new CapacityCounterResourceCapacityProcessingService(capacities: new CapacityCounterService()));
    private readonly ResourceBudget budget;
    public StorageResourceLimiterExposure(StorageLimits storageLimits)
    {
        budget = service.PrepareResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Limits = storageLimits })
            .Budget!;
    }

    public StorageLimits GetLimits() =>
        service.GetResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget })
            .Limits!;

    public int GetActiveTransactions() =>
        service.ReadActiveResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.Transactions });

    public int GetActivePins() =>
        service.ReadActiveResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.Pins });

    public ResourceReservation AcquireTransaction() =>
        new ResourceReservation
    {
        Request = service.AcquireResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.Transactions })
    };

    public ResourceReservation AcquirePin() =>
        new ResourceReservation
    {
        Request = service.AcquireResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.Pins })
    };

    public void ReleaseReservation(ResourceReservation resourceReservation) =>
        service.ReleaseResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Reservation = resourceReservation });

    public void ValidateRowLength(long length) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.RowBytes, Value = length });

    public void ValidateKeyLength(long length) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.KeyBytes, Value = length });

    public void ValidateValueLength(long length) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.ValueBytes, Value = length });

    public void ValidateOverflowPages(long count) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.OverflowPages, Value = count });

    public void ValidateUndoBytes(long length) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.UndoBytes, Value = length });

    public void ValidateScanPages(long count) =>
        service.CheckResourceLimitRequest(resourceLimitRequest: new ResourceLimitRequest { Budget = budget, Kind = ResourceLimitKind.ScanPages, Value = count });
}
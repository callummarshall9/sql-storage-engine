// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Models.Resources;
/// <summary>Carries configuration, a quota query, or one reservation against a private shared budget.</summary>
internal sealed class ResourceLimitRequest
{
    public StorageLimits? Limits { get; init; }
    public ResourceBudget? Budget { get; init; }
    public ResourceLimitKind Kind { get; init; }
    public long Value { get; init; }
    public CapacityCounter? ReservationCounter { get; set; }
    public ResourceReservation? Reservation { get; init; }

    public int Released;
}
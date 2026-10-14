// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Resources;
/// <summary>An opaque reservation issued by one resource limiter; release it through that limiter.</summary>
public sealed class ResourceReservation
{
    internal ResourceLimitRequest? Request { get; init; }
}
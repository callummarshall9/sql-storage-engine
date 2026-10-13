// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Services.Processings.LockRules;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Exposures.Locking;
/// <summary>Exposes lock compatibility and conversion through the lock-rules processing contract.</summary>
public static class LockRulesExposure
{
    private static readonly ILockRulesProcessingService service = new LockRulesProcessingService();
    public static bool AreCompatible(LockMode first, LockMode second) =>
        service.AreCompatible(first: first, second: second);

    public static bool CanConvert(LockMode current, LockMode requested) =>
        service.CanConvert(current: current, requested: requested);

    public static void EnsureValidConversion(LockMode current, LockMode requested) =>
        service.EnsureValidConversion(current: current, requested: requested);
}
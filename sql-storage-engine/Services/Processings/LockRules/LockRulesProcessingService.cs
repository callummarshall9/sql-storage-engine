// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.LockRules;
/// <summary>Applies the lock compatibility matrix and the defined upgrade paths.</summary>
internal sealed partial class LockRulesProcessingService : ILockRulesProcessingService
{
    public bool AreCompatible(LockMode first, LockMode second) =>
        TryCatch(operation: () =>
    {
        ValidateModes(first: first, second: second);

        return (first, second) switch
        {
            (LockMode.Shared, LockMode.Shared or LockMode.Update) => true,
            (LockMode.Update, LockMode.Shared) => true,
            _ => false
        };
    });

    public bool CanConvert(LockMode current, LockMode requested) =>
        TryCatch(operation: () =>
    {
        ValidateConversionModes(current: current, requested: requested);
        return CanConvertCore(current: current, requested: requested);
    });

    public void EnsureValidConversion(LockMode current, LockMode requested) =>
        TryCatch(operation: () =>
    {
        ValidateConversionModes(current: current, requested: requested);
        ValidateConversion(current: current, requested: requested);
    });

    private static bool CanConvertCore(LockMode current, LockMode requested) =>
        current == requested || (current, requested)is (LockMode.Shared, LockMode.Update or LockMode.Exclusive) or (LockMode.Update, LockMode.Exclusive);
}
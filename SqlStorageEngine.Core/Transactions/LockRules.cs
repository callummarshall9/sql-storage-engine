using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Defines the compatibility and conversion rules shared by all lock-manager implementations.</summary>
public static class LockRules
{
    /// <summary>Returns whether two modes may be granted to different transactions concurrently.</summary>
    public static bool AreCompatible(LockMode first, LockMode second)
    {
        Validate(first, nameof(first));
        Validate(second, nameof(second));
        return (first, second) switch
        {
            (LockMode.Shared, LockMode.Shared or LockMode.Update) => true,
            (LockMode.Update, LockMode.Shared) => true,
            _ => false
        };
    }

    /// <summary>Returns whether an owner may convert directly between the supplied modes.</summary>
    public static bool CanConvert(LockMode current, LockMode requested)
    {
        Validate(current, nameof(current));
        Validate(requested, nameof(requested));
        return current == requested || (current, requested) is
            (LockMode.Shared, LockMode.Update or LockMode.Exclusive) or
            (LockMode.Update, LockMode.Exclusive);
    }

    /// <summary>Rejects a conversion that would weaken a lock or bypass the defined upgrade paths.</summary>
    public static void EnsureValidConversion(LockMode current, LockMode requested)
    {
        if (!CanConvert(current, requested))
            throw new InvalidOperationException($"A {current} lock cannot be converted to {requested}.");
    }

    private static void Validate(LockMode mode, string parameterName)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(parameterName);
    }
}

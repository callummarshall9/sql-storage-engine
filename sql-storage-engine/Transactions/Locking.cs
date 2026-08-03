using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

/// <summary>Describes the access granted to a transaction for one logical resource.</summary>
public enum LockMode
{
    Shared = 1,
    Update = 2,
    Exclusive = 3
}

/// <summary>Identifies a logical resource independently of its in-memory or on-disk location.</summary>
public abstract record LockResource;

/// <summary>Identifies all rows and indexes belonging to a table.</summary>
public sealed record TableLockResource(TableId TableId) : LockResource;

/// <summary>Identifies one generation-safe row within a table.</summary>
public sealed record RowLockResource(TableId TableId, RowId RowId) : LockResource;

/// <summary>Identifies one encoded key in an index.</summary>
public sealed record IndexKeyLockResource : LockResource
{
    public IndexKeyLockResource(IndexId indexId, IndexKey key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        IndexId = indexId;
    }

    public IndexId IndexId { get; }
    public IndexKey Key { get; }
}

/// <summary>Identifies an index interval; a null endpoint represents an unbounded side.</summary>
public sealed record IndexRangeLockResource : LockResource
{
    public IndexRangeLockResource(IndexId indexId, IndexKey? lowerBound, IndexKey? upperBound,
        bool includeLowerBound = true, bool includeUpperBound = true)
    {
        if (lowerBound is not null && upperBound is not null && lowerBound.CompareTo(upperBound) > 0)
            throw new ArgumentException("The lower range bound cannot exceed the upper range bound.");
        IndexId = indexId;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        IncludeLowerBound = lowerBound is not null && includeLowerBound;
        IncludeUpperBound = upperBound is not null && includeUpperBound;
    }

    public IndexId IndexId { get; }
    public IndexKey? LowerBound { get; }
    public IndexKey? UpperBound { get; }
    public bool IncludeLowerBound { get; }
    public bool IncludeUpperBound { get; }

    /// <summary>Gets whether equal finite endpoints exclude the only possible key.</summary>
    public bool IsEmpty => LowerBound is not null && UpperBound is not null &&
        LowerBound.Equals(UpperBound) && (!IncludeLowerBound || !IncludeUpperBound);

    /// <summary>Creates a lock interval with endpoint semantics identical to a B+ tree scan range.</summary>
    public static IndexRangeLockResource From(IndexId indexId, IndexRange range) =>
        new(indexId, range.LowerBound, range.UpperBound, range.IncludeLowerBound, range.IncludeUpperBound);
}

/// <summary>Defines equality, range overlap, and insertion-intent conflict domains for logical resources.</summary>
public static class LockResourceRelations
{
    public static bool Conflict(LockResource first, LockResource second) => (first, second) switch
    {
        (IndexRangeLockResource left, IndexRangeLockResource right) => Overlap(left, right),
        (IndexRangeLockResource range, IndexKeyLockResource key) => Contains(range, key),
        (IndexKeyLockResource key, IndexRangeLockResource range) => Contains(range, key),
        _ => first.Equals(second)
    };

    public static bool Overlap(IndexRangeLockResource first, IndexRangeLockResource second)
    {
        if (first.IndexId != second.IndexId || first.IsEmpty || second.IsEmpty) return false;
        return !Before(first.UpperBound, first.IncludeUpperBound, second.LowerBound, second.IncludeLowerBound) &&
               !Before(second.UpperBound, second.IncludeUpperBound, first.LowerBound, first.IncludeLowerBound);
    }

    public static bool Contains(IndexRangeLockResource range, IndexKeyLockResource key)
    {
        if (range.IndexId != key.IndexId || range.IsEmpty) return false;
        var aboveLower = range.LowerBound is null || key.Key.CompareTo(range.LowerBound) > 0 ||
            key.Key.Equals(range.LowerBound) && range.IncludeLowerBound;
        var belowUpper = range.UpperBound is null || key.Key.CompareTo(range.UpperBound) < 0 ||
            key.Key.Equals(range.UpperBound) && range.IncludeUpperBound;
        return aboveLower && belowUpper;
    }

    private static bool Before(IndexKey? upper, bool includeUpper, IndexKey? lower, bool includeLower)
    {
        if (upper is null || lower is null) return false;
        var comparison = upper.CompareTo(lower);
        return comparison < 0 || comparison == 0 && !(includeUpper && includeLower);
    }
}

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

/// <summary>
/// Coordinates transaction-owned logical locks. Acquisition and conversion wait until grantable and honor
/// cancellation without retaining a request. A transaction owns each granted lock until explicit release or
/// <see cref="ReleaseAll"/>; implementations must not transfer ownership between transaction IDs.
/// </summary>
public interface ILockManager
{
    ValueTask AcquireAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default);
    ValueTask ConvertAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default);
    bool Release(TransactionId transactionId, LockResource resource);
    void ReleaseAll(TransactionId transactionId);
}

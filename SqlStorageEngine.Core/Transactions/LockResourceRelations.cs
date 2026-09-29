using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

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

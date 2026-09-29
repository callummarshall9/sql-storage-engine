using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Transactions;

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

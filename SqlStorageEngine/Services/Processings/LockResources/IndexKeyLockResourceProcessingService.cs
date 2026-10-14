// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Services.Foundations.IndexKeys;

namespace sql_storage_engine.Services.Processings.LockResources;
internal sealed partial class IndexKeyLockResourceProcessingService(IIndexKeyService keys) : IIndexKeyLockResourceProcessingService
{
    public LockResource PrepareLockResource(LockResource lockResource) =>
        TryCatch(operation: () =>
    {
        ValidateLockResource(lockResource: lockResource);

        return lockResource is IndexRangeLockResource range ? range with
        {
            IncludeLowerBound = range.LowerBound is not null && range.IncludeLowerBound,
            IncludeUpperBound = range.UpperBound is not null && range.IncludeUpperBound
        }

        : lockResource;
    });

    public bool CheckLockResourceConflict(LockResource[] lockResources) =>
        TryCatch(operation: () =>
    {
        ValidateLockResources(lockResources: lockResources);
        return Conflict(first: lockResources[0], second: lockResources[1]);
    });

    public bool ReadLockResourceEmpty(LockResource lockResource) =>
        TryCatch(operation: () =>
    {
        ValidateLockResource(lockResource: lockResource);
        return lockResource is IndexRangeLockResource range && IsEmptyRange(range: range);
    });

    private static bool IsEmptyRange(IndexRangeLockResource range) =>
        range.LowerBound is not null && range.UpperBound is not null && range.LowerBound.Equals(other: range.UpperBound) && (!range.IncludeLowerBound || !range.IncludeUpperBound);

    private bool Conflict(LockResource first, LockResource second) =>
        (first, second) switch
    {
        (IndexRangeLockResource left, IndexRangeLockResource right) => Overlap(first: left, second: right),
        (IndexRangeLockResource range, IndexKeyLockResource key) => Contains(range: range, key: key),
        (IndexKeyLockResource key, IndexRangeLockResource range) => Contains(range: range, key: key),
        _ => first.Equals(other: second)};

    private bool Overlap(IndexRangeLockResource first, IndexRangeLockResource second)
    {
        if (first.IndexId != second.IndexId || IsEmptyRange(range: first) || IsEmptyRange(range: second))
        {
            return false;
        }

        return !Before(upper: first.UpperBound, includeUpper: first.IncludeUpperBound, lower: second.LowerBound, includeLower: second.IncludeLowerBound) && !Before(upper: second.UpperBound, includeUpper: second.IncludeUpperBound, lower: first.LowerBound, includeLower: first.IncludeLowerBound);
    }

    private bool Contains(IndexRangeLockResource range, IndexKeyLockResource key)
    {
        if (range.IndexId != key.IndexId || IsEmptyRange(range: range))
        {
            return false;
        }

        var aboveLower = range.LowerBound is null || keys.CompareIndexKeys(indexKeys: [key.Key, range.LowerBound]) > 0 || key.Key.Equals(other: range.LowerBound) && range.IncludeLowerBound;
        var belowUpper = range.UpperBound is null || keys.CompareIndexKeys(indexKeys: [key.Key, range.UpperBound]) < 0 || key.Key.Equals(other: range.UpperBound) && range.IncludeUpperBound;
        return aboveLower && belowUpper;
    }

    private bool Before(sql_storage_engine.Models.Indexes.IndexKey? upper, bool includeUpper, sql_storage_engine.Models.Indexes.IndexKey? lower, bool includeLower)
    {
        if (upper is null || lower is null)
        {
            return false;
        }

        var comparison = keys.CompareIndexKeys(indexKeys: [upper, lower]);
        return comparison < 0 || comparison == 0 && !(includeUpper && includeLower);
    }
}
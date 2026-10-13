// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.LockResources;

namespace sql_storage_engine.Services.Processings.LockResources;
internal sealed partial class IndexKeyLockResourceProcessingService
{
    private void ValidateLockResource(LockResource lockResource)
    {
        ArgumentNullException.ThrowIfNull(argument: lockResource);

        if (lockResource is IndexKeyLockResource key)
        {
            ArgumentNullException.ThrowIfNull(argument: key.Key);
            keys.CheckIndexKey(indexKey: key.Key);
        }

        if (lockResource is IndexRangeLockResource range)
        {
            if (range.LowerBound is not null)
            {
                keys.CheckIndexKey(indexKey: range.LowerBound);
            }

            if (range.UpperBound is not null)
            {
                keys.CheckIndexKey(indexKey: range.UpperBound);
            }

            if (range.LowerBound is not null && range.UpperBound is not null && keys.CompareIndexKeys(indexKeys: [range.LowerBound, range.UpperBound]) > 0)
            {
                throw new ArgumentException(message: "The lower range bound cannot exceed the upper range bound.");
            }
        }
    }

    private void ValidateLockResources(LockResource[] lockResources)
    {
        ArgumentNullException.ThrowIfNull(argument: lockResources);

        if (lockResources.Length != 2)
        {
            throw new ArgumentException(message: "A conflict comparison requires exactly two lock resources.", paramName: nameof(lockResources));
        }

        foreach (var lockResource in lockResources)
        {
            ValidateLockResource(lockResource: lockResource);
        }
    }
}
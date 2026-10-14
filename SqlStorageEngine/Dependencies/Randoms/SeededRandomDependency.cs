// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Dependencies.Randoms;
internal sealed class SeededRandomDependency(int seed) : Random(seed)
{
    /// <summary>Offset generation requires a nonempty target interval, unlike Random.Next(0).</summary>
    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: maxValue);
        return base.Next(maxValue: maxValue);
    }

    public int[] GenerateOffsets(int maximum, int count)
    {
        var offsets = new int[count];

        for (var index = 0; index < count; index++)
        {
            offsets[index] = Next(maxValue: maximum);
        }

        return offsets;
    }
}
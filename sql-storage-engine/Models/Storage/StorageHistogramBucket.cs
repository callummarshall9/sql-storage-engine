// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageHistogramBucket
{
    private IReadOnlyList<SqlValue>? upperBound;
    public IReadOnlyList<SqlValue> UpperBound { get => upperBound!; set => upperBound = value; }
    public long RangeRows { get; set; }
    public long EqualRows { get; set; }
    public long DistinctRangeValues { get; set; }
}
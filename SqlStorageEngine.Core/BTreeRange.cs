namespace sql_storage_engine;

public sealed record BTreeRange<TKey>(
    TKey LowerBound,
    TKey UpperBound,
    bool IncludeLowerBound = true,
    bool IncludeUpperBound = true,
    ScanDirection Direction = ScanDirection.Ascending);

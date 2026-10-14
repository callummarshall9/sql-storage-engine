// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models;
public sealed record BTreeRange<TKey>
{
    private TKey? _lowerBound;
    public TKey LowerBound { get => _lowerBound!; init => _lowerBound = value; }

    private TKey? _upperBound;
    public TKey UpperBound { get => _upperBound!; init => _upperBound = value; }
    public bool IncludeLowerBound { get; init; }
    public bool IncludeUpperBound { get; init; }
    public ScanDirection Direction { get; init; }
}
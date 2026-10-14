// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Indexes;
public readonly record struct IndexRange
{
    private readonly sql_storage_engine.Models.Indexes.IndexKey? _lowerBound;
    public sql_storage_engine.Models.Indexes.IndexKey? LowerBound { get => _lowerBound!; init => _lowerBound = value; }

    private readonly sql_storage_engine.Models.Indexes.IndexKey? _upperBound;
    public sql_storage_engine.Models.Indexes.IndexKey? UpperBound { get => _upperBound!; init => _upperBound = value; }
    public bool IncludeLowerBound { get; init; }
    public bool IncludeUpperBound { get; init; }
    public ScanDirection Direction { get; init; }
}
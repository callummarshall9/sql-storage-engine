// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Models;
/// <summary>An index range with independently optional lower/upper bounds.</summary>
public sealed record StorageIndexRange
{
    public StorageIndexBound? LowerBound { get => _lowerBound!; init => _lowerBound = value; }
    public StorageIndexBound? UpperBound { get => _upperBound!; init => _upperBound = value; }

    private StorageIndexBound? _lowerBound;
    private StorageIndexBound? _upperBound;
    public ScanDirection Direction { get; init; }
}
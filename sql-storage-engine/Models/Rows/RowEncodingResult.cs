// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Models.Rows;
public sealed record RowEncodingResult
{
    public byte[] Bytes { get => _bytes!; init => _bytes = value; }
    public IReadOnlyList<OverflowReference> NewlyAllocated { get => _newlyAllocated!; init => _newlyAllocated = value; }
    public IReadOnlyList<OverflowReference> Retired { get => _retired!; init => _retired = value; }

    private byte[]? _bytes;
    private IReadOnlyList<OverflowReference>? _newlyAllocated;
    private IReadOnlyList<OverflowReference>? _retired;
}
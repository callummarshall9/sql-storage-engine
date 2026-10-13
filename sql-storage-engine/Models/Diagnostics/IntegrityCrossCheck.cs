// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record IntegrityCrossCheck
{
    public IReadOnlySet<RowId> HeapRows { get => _heapRows!; init => _heapRows = value; }
    public IReadOnlySet<RowId> IndexRows { get => _indexRows!; init => _indexRows = value; }

    private IReadOnlySet<RowId>? _heapRows;
    private IReadOnlySet<RowId>? _indexRows;
}
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageReadOptions
{
    public bool CanUnmaskAll { get; init; }
    public IReadOnlySet<ColumnId> UnmaskedColumns { get => unmaskedColumns!; init => unmaskedColumns = value; }

    private IReadOnlySet<ColumnId>? unmaskedColumns;
}
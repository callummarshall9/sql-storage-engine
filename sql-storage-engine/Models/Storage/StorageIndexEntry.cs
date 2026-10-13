// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageIndexEntry
{
    public RowId RowId { get; init; }
    public IReadOnlyDictionary<ColumnId, SqlValue> IncludedValues { get => includedValues!; init => includedValues = value; }

    private IReadOnlyDictionary<ColumnId, SqlValue>? includedValues;
}
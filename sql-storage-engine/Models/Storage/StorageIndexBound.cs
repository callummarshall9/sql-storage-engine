// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageIndexBound
{
    public IReadOnlyList<SqlValue> Values { get => values!; init => values = value; }

    private IReadOnlyList<SqlValue>? values;
    public bool Inclusive { get; init; }
}
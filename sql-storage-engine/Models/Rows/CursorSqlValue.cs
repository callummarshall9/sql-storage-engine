// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record CursorSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public IReadOnlyList<Row> Rows { get => rows!; init => rows = value; }

    private IReadOnlyList<Row>? rows;
    public int Position { get; init; }
    public bool IsOpen { get; init; }
    public Row? CurrentRow { get; init; }
}
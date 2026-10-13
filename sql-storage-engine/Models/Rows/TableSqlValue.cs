// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record TableSqlValue : SqlValue
{
    public override SqlStorageFamily? StorageFamily { get; init; }
    public TableDefinition Definition { get => definition!; init => definition = value; }
    public System.Collections.Generic.IReadOnlyList<Row> Rows { get => rows!; init => rows = value; }

    private TableDefinition? definition;
    private System.Collections.Generic.IReadOnlyList<Row>? rows;
}
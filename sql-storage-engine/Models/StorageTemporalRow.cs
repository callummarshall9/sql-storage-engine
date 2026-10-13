// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models;
/// <summary>A temporal row with an unambiguous current or history table identity.</summary>
public sealed record StorageTemporalRow
{
    public TableId SourceTableId { get; init; }
    public RowId RowId { get; init; }
    public Row Row { get => _row!; init => _row = value; }

    private Row? _row;
}
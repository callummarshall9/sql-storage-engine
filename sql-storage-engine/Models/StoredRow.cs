// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models;
/// <summary>A row and its opaque, generation-safe physical identity.</summary>
public sealed record StoredRow
{
    public RowId RowId { get; init; }
    public Row Row { get => _row!; init => _row = value; }

    private Row? _row;
}
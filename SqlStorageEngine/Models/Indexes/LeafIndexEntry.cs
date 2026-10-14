// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Indexes;
public readonly record struct LeafIndexEntry
{
    private readonly IndexKey? key;
    private readonly string? payloadContent;
    public IndexKey Key { get => key!; init => key = value; }
    public RowId RowId { get; init; }
    public string PayloadContent { get => payloadContent!; init => payloadContent = value; }
}
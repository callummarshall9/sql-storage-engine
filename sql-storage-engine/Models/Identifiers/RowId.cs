// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
public readonly record struct RowId
{
    public PageId PageId { get; init; }
    public SlotId SlotId { get; init; }
    public SlotGeneration Generation { get; init; }
}
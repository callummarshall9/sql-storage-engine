namespace sql_storage_engine.Identifiers;

public readonly record struct RowId(PageId PageId, SlotId SlotId, SlotGeneration Generation)
{
    public override string ToString()
        => $"row:{PageId.Value}/{SlotId.Value}/{Generation.Value}";
}

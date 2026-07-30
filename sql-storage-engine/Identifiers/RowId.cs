namespace sql_storage_engine.Identifiers;

public readonly record struct RowId(PageId PageId, SlotId SlotId, SlotGeneration SlotGeneration)
{
    public override string ToString()
        => "PageId:" + PageId + ":SlotId:" + SlotId + ":SlotGeneration:" + SlotGeneration;
}
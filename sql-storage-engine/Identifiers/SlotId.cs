namespace sql_storage_engine.Identifiers;

public readonly record struct SlotId(ushort Value)
{
    public override string ToString()
        => $"slot:{Value}";
}

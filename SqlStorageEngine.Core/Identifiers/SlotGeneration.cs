namespace sql_storage_engine.Identifiers;

public readonly record struct SlotGeneration(uint Value)
{
    public override string ToString()
        => $"generation:{Value}";
}

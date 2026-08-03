namespace sql_storage_engine.Identifiers;

public readonly record struct TableId(ulong Value)
{
    public override string ToString()
        => $"table:{Value}";
}

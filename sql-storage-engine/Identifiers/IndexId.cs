namespace sql_storage_engine.Identifiers;

public readonly record struct IndexId(ulong Value)
{
    public override string ToString()
        => $"index:{Value}";
}

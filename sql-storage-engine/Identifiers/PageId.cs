namespace sql_storage_engine.Identifiers;

public readonly record struct PageId(ulong Value)
{
    public override string ToString()
        => $"page:{Value}";
}

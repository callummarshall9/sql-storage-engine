namespace sql_storage_engine.Identifiers;

public readonly record struct TransactionId(ulong Value)
{
    public override string ToString()
        => $"transaction:{Value}";
}

namespace sql_storage_engine.Identifiers;

public readonly record struct LogSequenceNumber(ulong Value)
{
    public override string ToString()
        => "LogSequenceNumber:" + Value;
}
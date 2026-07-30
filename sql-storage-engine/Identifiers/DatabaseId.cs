namespace sql_storage_engine.Identifiers;

public readonly record struct DatabaseId(Guid Value)
{
    public static DatabaseId New()
        => new DatabaseId(Guid.NewGuid());

    public override string ToString()
        => "DatabaseId:" + Value;
}
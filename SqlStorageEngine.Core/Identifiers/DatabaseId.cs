namespace sql_storage_engine.Identifiers;

public readonly record struct DatabaseId(Guid Value)
{
    /// <summary>Creates a new globally unique database identifier.</summary>
    public static DatabaseId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => $"database:{Value:D}";
}
